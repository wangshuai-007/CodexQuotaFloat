using System.Text.Json;
using QuotaFloat.Core;

internal static class HookStatusTests
{
    internal static async Task<int> RunAsync()
    {
        var count = 0;
        void Check(string name, bool value)
        {
            if (!value) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name); count++;
        }
        using var config = JsonDocument.Parse("""{"config":{"features":{"hooks":true}}}""");
        string Hook(string trust, bool enabled = true, string command = "expected", string source = "user") =>
            JsonSerializer.Serialize(new { statusMessage = CodexSessionHook.Marker, eventName = "sessionStart",
                handlerType = "command", source, command, enabled, trustStatus = trust });
        CodexHookStatus Parse(string hook, string errors = "[]", JsonElement? cfg = null)
        {
            using var json = JsonDocument.Parse($$"""{"data":[{"errors":{{errors}},"hooks":[{{hook}}]}]}""");
            return CodexHookInspector.Parse(json.RootElement, cfg ?? config.RootElement, "expected");
        }
        Check("仅启用且精确匹配已信任钩子就绪", Parse(Hook("trusted")).Ready);
        Check("新钩子提示审核", Parse(Hook("untrusted")).Code == "untrusted");
        Check("修改钩子提示重新信任", Parse(Hook("modified")).Code == "modified");
        Check("已信任但禁用不就绪", Parse(Hook("trusted", false)).Code == "disabled");
        Check("未知信任状态不伪造就绪", Parse(Hook("future")).Code == "unknown");
        Check("同名旧命令不冒充当前程序", Parse(Hook("trusted", command: "old")).Code == "changed");
        Check("项目同名钩子不冒充用户钩子", Parse(Hook("trusted", source: "project")).Code == "missing");
        Check("重复同名钩子不任取可信结果", Parse(Hook("trusted") + "," + Hook("modified")).Code == "changed");
        Check("未安装提示保存后配置", Parse("").Code == "missing");
        Check("配置解析失败不标记就绪", Parse(Hook("trusted"), "[{}]").Code == "configurationError");
        using var off = JsonDocument.Parse("""{"config":{"features":{"hooks":false}}}""");
        Check("全局禁用优先于信任", Parse(Hook("trusted"), cfg: off.RootElement).Code == "featureDisabled");
        using var legacy = JsonDocument.Parse("""{"config":{"features":{"codex_hooks":false}}}""");
        Check("识别旧配置关闭钩子", Parse(Hook("trusted"), cfg: legacy.RootElement).Code == "featureDisabled");
        using var malformed = JsonDocument.Parse("{}");
        Check("未知有效配置不标记就绪", Parse(Hook("trusted"), cfg: malformed.RootElement).Code == "unknown");
        Check("未知列表协议不标记就绪", CodexHookInspector.Parse(malformed.RootElement, config.RootElement, "expected").Code == "unknown");
        var exe = Path.Combine(AppContext.BaseDirectory, "CodexQuotaFloat.exe");
        var fixture = Path.Combine(AppContext.BaseDirectory, "QuotaFloat.Tests.exe");
        var live = await CodexHookInspector.CheckAsync(exe, cli: fixture);
        Check("真实stdio只读配置及信任查询链路", live.Code == "modified");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await CodexHookInspector.CheckAsync(exe, cancelled.Token, fixture); Check("检测取消传播", false); }
        catch (OperationCanceledException) { Check("检测取消传播", true); }
        var unavailable = await CodexHookInspector.CheckAsync(exe, cli: Path.Combine(AppContext.BaseDirectory, "missing.exe"));
        Check("CLI不可用降级未知而非就绪", unavailable.Code == "unknown" && !unavailable.Ready);
        var original = Environment.GetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC");
        try
        {
            Environment.SetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC", "1");
            var invalid = await CodexHookInspector.CheckAsync(exe, cli: fixture);
            Check("信任检测协议异常不泄露正文或误报就绪", invalid.Code == "unknown" && !invalid.Message.Contains("private-protocol"));
        }
        finally { Environment.SetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC", original); }
        return count;
    }
}
