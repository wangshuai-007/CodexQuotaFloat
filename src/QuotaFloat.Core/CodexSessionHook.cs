using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace QuotaFloat.Core;

/// <summary>维护本工具的用户级SessionStart启动检查，保留其他钩子及配置字段。</summary>
public static class CodexSessionHook
{
    private const string Marker = "CodexQuotaFloat: 检查浮窗启动";

    /// <summary>按跟随开关安装或移除自身钩子；修改前备份，损坏配置保持原样并报错。</summary>
    public static bool Configure(bool enabled, string executable, string? codexHome = null)
    {
        var home = codexHome ?? Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(home))
            home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var path = Path.Combine(Path.GetFullPath(home), "hooks.json");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
        using var gate = new Mutex(false, "Local\\CodexQuotaFloat.Hooks." + hash);
        var acquired = false;
        try
        {
            try { acquired = gate.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("启动钩子配置正被使用，请稍后重试。");
            return Update(path, enabled, Path.GetFullPath(executable));
        }
        finally { if (acquired) gate.ReleaseMutex(); }
    }

    private static bool Update(string path, bool enabled, string executable)
    {
        if (enabled && !File.Exists(executable)) throw new IOException("找不到浮窗程序，无法配置会话启动检查。");
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        if (original is null && !enabled) return false;
        var root = original is null ? new JsonObject() : JsonNode.Parse(original) as JsonObject
            ?? throw new JsonException("hooks.json必须为JSON对象。");
        if (!enabled && root["hooks"] is null) return false;
        var before = root.DeepClone();
        var hooks = ObjectProperty(root, "hooks");
        var groups = hooks["SessionStart"] as JsonArray;
        if (hooks["SessionStart"] is not null && groups is null)
            throw new JsonException("SessionStart必须为数组。");
        groups ??= new JsonArray();
        RemoveOwned(groups);
        if (enabled) groups.Add(CreateGroup(executable));
        if (groups.Count > 0) hooks["SessionStart"] = groups.Parent is null ? groups : groups.DeepClone();
        else hooks.Remove("SessionStart");
        if (JsonNode.DeepEquals(before, root)) return false;
        WriteAtomic(path, original, root);
        return true;
    }

    private static JsonObject ObjectProperty(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject value) return value;
        if (parent[name] is not null) throw new JsonException(name + "必须为JSON对象。");
        var created = new JsonObject();
        parent[name] = created;
        return created;
    }

    private static void RemoveOwned(JsonArray groups)
    {
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] is not JsonObject group || group["hooks"] is not JsonArray handlers)
                throw new JsonException("SessionStart分组缺少hooks数组。");
            var removed = false;
            for (var j = handlers.Count - 1; j >= 0; j--)
                if (handlers[j] is JsonObject handler && handler["type"]?.ToString() == "command" &&
                    handler["statusMessage"]?.ToString() == Marker)
                { handlers.RemoveAt(j); removed = true; }
            if (removed && handlers.Count == 0) groups.RemoveAt(i);
        }
    }

    private static JsonObject CreateGroup(string executable) => new()
    {
        ["matcher"] = "^(startup|resume|clear|compact|fork)$",
        ["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command", ["command"] = BuildCommand(executable),
            ["timeout"] = 10, ["statusMessage"] = Marker
        })
    };

    /// <summary>生成不依赖会话工作目录的Windows命令；检查单实例锁后脱离启动，不输出模型上下文。</summary>
    public static string BuildCommand(string executable)
    {
        var escaped = Path.GetFullPath(executable).Replace("'", "''");
        var script = "# CodexQuotaFloat SessionStart\n$ErrorActionPreference = 'Stop'\n" +
            "$lock = $null\ntry {\n" +
            "  if ([System.Threading.Mutex]::TryOpenExisting('Local\\CodexQuotaFloat', [ref]$lock)) { return }\n" +
            $"  Start-Process -FilePath '{escaped}' -ArgumentList '--session-start' -WindowStyle Hidden | Out-Null\n" +
            "} catch { [Console]::Error.WriteLine('CodexQuotaFloat startup check failed: ' + $_.Exception.GetType().Name); exit 1 }\n" +
            "finally { if ($null -ne $lock) { $lock.Dispose() } }\n";
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return $"\"{shell}\" -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }

    private static void WriteAtomic(string path, string? original, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            // 不覆盖本工具读取之后被外部编辑器更改的配置。
            if ((File.Exists(path) ? File.ReadAllText(path) : null) != original)
                throw new IOException("hooks.json已被其他程序修改，请重新保存。");
            if (original is not null)
                File.Copy(path, path + ".CodexQuotaFloat." + Guid.NewGuid().ToString("N") + ".bak");
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
