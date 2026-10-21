using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QuotaFloat.Core;

internal static class SessionHookTests
{
    internal static int Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "QuotaFloat-Hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { return CheckFiles(folder); }
        finally { Directory.Delete(folder, true); }
    }

    private static int CheckFiles(string folder)
    {
        var passed = 0;
        void Check(string label, bool value)
        {
            if (!value) throw new InvalidOperationException("失败：" + label);
            Console.WriteLine("通过：" + label); passed++;
        }
        var exe = Path.Combine(folder, "测试 ' $ 目录.exe");
        File.WriteAllText(exe, "fixture");
        var path = Path.Combine(folder, "hooks.json");
        Check("关闭跟随不会创建不存在的钩子配置", !CodexSessionHook.Configure(false, exe, folder) && !File.Exists(path));
        Check("开启跟随创建会话启动检查", CodexSessionHook.Configure(true, exe, folder));
        var first = File.ReadAllText(path);
        Check("重复开启不改写或重复添加", !CodexSessionHook.Configure(true, exe, folder) && first == File.ReadAllText(path));
        Check("钩子UTF8无BOM", !File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
        var root = JsonNode.Parse(first)!;
        var handler = root["hooks"]!["SessionStart"]![0]!["hooks"]![0]!;
        var command = handler["command"]!.ToString();
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(command.Split(' ')[^1]));
        Check("启动检查不依赖cwd且正确转义中文单引号路径", script.Contains(exe.Replace("'", "''")));
        Check("启动检查使用单实例锁和静默参数", script.Contains("TryOpenExisting") && script.Contains("--session-start") && script.Contains("-WindowStyle Hidden"));
        Check("钩子有短超时", handler["timeout"]!.GetValue<int>() == 10);
        Check("关闭时移除自身启动检查", CodexSessionHook.Configure(false, exe, folder) && !File.ReadAllText(path).Contains("SessionStart"));
        passed += CheckPreservation(folder, exe);
        return passed;
    }

    private static int CheckPreservation(string folder, string exe)
    {
        var passed = 0;
        void Check(string label, bool value)
        {
            if (!value) throw new InvalidOperationException("失败：" + label);
            Console.WriteLine("通过：" + label); passed++;
        }
        var path = Path.Combine(folder, "hooks.json");
        const string original = """{"description":"保留","custom":{"x":1},"hooks":{"SessionStart":[{"hooks":[{"type":"command","command":"echo other"}]}],"Stop":[{"hooks":[{"type":"command","command":"echo stop"}]}]}}""";
        File.WriteAllText(path, original);
        CodexSessionHook.Configure(true, exe, folder);
        CodexSessionHook.Configure(false, exe, folder);
        Check("添加移除保留其他事件及未知字段", JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(File.ReadAllText(path))));
        Check("修改前备份原配置", Directory.GetFiles(folder, "*.bak").Any(p => File.ReadAllText(p) == original));
        File.WriteAllText(path, "{ damaged");
        try { CodexSessionHook.Configure(true, exe, folder); throw new InvalidOperationException("未拒绝损坏配置"); }
        catch (JsonException) { Check("损坏配置不覆盖", File.ReadAllText(path) == "{ damaged"); }
        File.WriteAllText(path, "{\"hooks\":{\"SessionStart\":{}}}");
        try { CodexSessionHook.Configure(true, exe, folder); throw new InvalidOperationException("未拒绝错误结构"); }
        catch (JsonException) { Check("错误结构保持原样", File.ReadAllText(path) == "{\"hooks\":{\"SessionStart\":{}}}"); }
        File.WriteAllText(path, "{}");
        Check("关闭不改写无关配置", !CodexSessionHook.Configure(false, exe, folder) && File.ReadAllText(path) == "{}");
        try { CodexSessionHook.Configure(true, exe + ".missing", folder); throw new InvalidOperationException("未拒绝缺失EXE"); }
        catch (IOException) { Check("缺失EXE不安装无效钩子", File.ReadAllText(path) == "{}"); }
        return passed;
    }
}
