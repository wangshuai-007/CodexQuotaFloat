using System.ComponentModel;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>只读检测结论；Ready仅表示配置已启用且已信任，不代表会话已实际触发。</summary>
public sealed record CodexHookStatus(bool Ready, string Code, string Message);

/// <summary>通过Codex自身的配置和hooks/list读取状态，不修改信任或执行会话钩子。</summary>
public static class CodexHookInspector
{
    /// <summary>检测当前用户配置中的本工具钩子；超时或不支持协议时明确返回未知。</summary>
    public static async Task<CodexHookStatus> CheckAsync(string executable, CancellationToken token = default,
        string? cli = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            cli ??= LocalCodexCli.Find();
            if (cli is null) return Unknown("未找到原生 Codex CLI，无法确认信任状态；请在 Codex 的 /hooks 中检查。");
            using var rpc = new CodexRpc(cli);
            await rpc.InitializeAsync(timeout.Token, true).ConfigureAwait(false);
            using var config = await rpc.RequestAsync("config/read", new { includeLayers = false }, timeout.Token).ConfigureAwait(false);
            using var hooks = await rpc.RequestAsync("hooks/list", new
                { cwds = new[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) } }, timeout.Token).ConfigureAwait(false);
            return Parse(hooks.RootElement, config.RootElement, CodexSessionHook.BuildCommand(executable));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return Unknown("检测超时，信任状态未知；可点击重新检测。"); }
        catch (Exception error) when (error is IOException or FormatException or JsonException or
            Win32Exception or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        { return Unknown("无法读取 Codex 状态，可能是CLI版本不支持或配置无效；请在 /hooks 中检查。"); }
    }

    /// <summary>解析只读协议结果，严格匹配当前启动命令，不将同名旧钩子视为就绪。</summary>
    public static CodexHookStatus Parse(JsonElement hooks, JsonElement config, string expectedCommand)
    {
        if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty("config", out var effective) ||
            effective.ValueKind != JsonValueKind.Object) return Unknown("Codex 未返回有效配置，信任状态未知。");
        if (Disabled(config))
            return new(false, "featureDisabled", "Codex 已关闭 hooks 功能，关联启动不会执行；请检查用户或管理员配置。");
        if (!Array(hooks, "data", out var entries) || entries.GetArrayLength() != 1)
            return Unknown("Codex 未返回有效钩子列表，信任状态未知。");
        var entry = entries[0];
        if (!Array(entry, "errors", out var errors) || errors.GetArrayLength() > 0)
            return new(false, "configurationError", "Codex 钩子配置解析失败，无法确认可用性；请在 /hooks 中检查。");
        if (!Array(entry, "hooks", out var list)) return Unknown("Codex 钩子列表格式不支持，信任状态未知。");
        var own = list.EnumerateArray().Where(h => Text(h, "statusMessage") == CodexSessionHook.Marker &&
            Text(h, "eventName") == "sessionStart" && Text(h, "source") == "user").ToArray();
        if (own.Length == 0)
            return new(false, "missing", "尚未检测到启动钩子；勾选跟随并保存后会配置，再检测信任状态。");
        var matching = own.Where(h => Text(h, "command") == expectedCommand && Text(h, "handlerType") == "command").ToArray();
        if (matching.Length != 1 || own.Length != 1)
            return new(false, "changed", "钩子路径或命令与当前程序不一致；请保存跟随设置，随后重新审核信任。");
        var hook = matching[0];
        if (!hook.TryGetProperty("enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return Unknown("Codex 未返回启用状态，无法确认关联启动可用。");
        var trust = Text(hook, "trustStatus");
        if (!enabled.GetBoolean())
            return new(false, "disabled", $"启动钩子已禁用（信任状态：{TrustLabel(trust)}）；请在 /hooks 中启用并确认信任。");
        return trust switch
        {
            "trusted" => new(true, "ready", "已信任、已启用。关联启动已就绪；新会话首次发送消息后检查启动，不代表打开首页即触发。"),
            "modified" => new(false, "modified", "启动钩子已修改，需要重新审核信任；进入 Codex CLI 的 /hooks，找到 CodexQuotaFloat。"),
            "untrusted" => new(false, "untrusted", "启动钩子尚未信任；进入 Codex CLI 的 /hooks，审核并信任 CodexQuotaFloat。"),
            _ => Unknown("Codex 返回未知信任状态；请在 /hooks 中检查，未确认关联启动可用。")
        };
    }

    private static bool Disabled(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("config", out var config) ||
            config.ValueKind != JsonValueKind.Object || !config.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Object) return false;
        return features.TryGetProperty("hooks", out var hooks) ? hooks.ValueKind == JsonValueKind.False :
            features.TryGetProperty("codex_hooks", out var legacy) && legacy.ValueKind == JsonValueKind.False;
    }

    private static bool Array(JsonElement root, string name, out JsonElement value)
    {
        value = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array;
    }

    private static string? Text(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string TrustLabel(string? trust) => trust switch
        { "trusted" => "已信任", "modified" => "需重新信任", "untrusted" => "尚未信任", _ => "未知" };

    private static CodexHookStatus Unknown(string message) => new(false, "unknown", message);
}
