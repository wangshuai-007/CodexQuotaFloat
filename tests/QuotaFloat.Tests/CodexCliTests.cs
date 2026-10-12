using System.Text.Json;
using QuotaFloat.Core;

internal static class CodexCliTests
{
    internal static async Task<int> RunAsync()
    {
        var count = 0;
        void Check(string title, bool condition)
        {
            if (!condition) throw new InvalidOperationException("失败：" + title);
            Console.WriteLine("通过：" + title); count++;
        }
        var executable = Path.Combine(AppContext.BaseDirectory, "QuotaFloat.Tests.exe");
        using var source = new CodexCliQuotaSource(executable);
        var accounts = await source.ReadAsync(default);
        Check("CLI复用连接避免5秒刷新反复启动进程", (await source.ReadAsync(default)).Count == 1);
        Check("官方协议stdio初始化、账号和额度读取", accounts.Count == 1 && accounts[0].Name == "fixture@demo.local");
        Check("CLI协议窗口分钟数转换为5h/周", accounts[0].FiveHourRemaining == 80 && accounts[0].WeeklyRemaining == 45);
        Check("CLI协议重置时刻正确", accounts[0].FiveHourReset == DateTimeOffset.FromUnixTimeSeconds(2000000000));
        using var many = JsonDocument.Parse("""{"rateLimits":{"primary":null},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":35,"windowDurationMins":300,"resetsAt":2000000000},"secondary":{"usedPercent":65,"windowDurationMins":10080,"resetsAt":2000000000}}}}""");
        var mapped = CodexCliQuotaSource.Parse(many.RootElement, "fixture", "Team");
        Check("优先codex多计量桶", mapped.FiveHourRemaining == 65 && mapped.WeeklyRemaining == 35);
        using var missing = JsonDocument.Parse("{}");
        Check("CLI缺失窗口不显示满额", CodexCliQuotaSource.Parse(missing.RootElement, "fixture", "Team").Error is not null);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await new CodexCliQuotaSource(executable).ReadAsync(cancellation.Token); Check("CLI刷新可取消", false); }
        catch (OperationCanceledException) { Check("CLI刷新可取消且回收自建进程", true); }
        await CheckInvalidProtocolAsync(executable, Check);
        return count;
    }

    private static async Task CheckInvalidProtocolAsync(string executable, Action<string, bool> check)
    {
        var original = Environment.GetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC");
        try
        {
            Environment.SetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC", "1");
            using var source = new CodexCliQuotaSource(executable);
            try { await source.ReadAsync(default); check("拒绝异常CLI协议", false); }
            catch (FormatException error)
            { check("CLI协议失败独立提示不泄露正文", error.Message.StartsWith("Codex CLI未返回") && !error.Message.Contains("private-protocol")); }
        }
        finally { Environment.SetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC", original); }
    }
}
