using System.Text.Json;
using QuotaFloat.Core;

internal static class AdditionalWindowTests
{
    internal static int Run()
    {
        var count = 0;
        void Check(string label, bool valid)
        {
            if (!valid) throw new InvalidOperationException("失败：" + label);
            Console.WriteLine("通过：" + label); count++;
        }
        using var usage = JsonDocument.Parse("""{"plan_type":"free","rate_limit":{"primary_window":{"used_percent":25,"limit_window_seconds":2592000,"reset_at":2000000000}}}""");
        var monthly = CpaParser.Parse(usage.RootElement, "fixture", "fixture@demo.local", "free");
        Check("月额度按真实周期和比例读取", monthly.AdditionalWindows is [ { Seconds: 2592000, Remaining: 75 } ]);
        Check("月窗口不冒充5h/周额度", monthly.FiveHourRemaining is null && monthly.WeeklyRemaining is null);
        Check("月窗口不计入双额度水位", monthly.Available(DateTimeOffset.UtcNow) is null);
        Check("月窗口包含重置时刻及明确标签", monthly.AdditionalWindows[0].Reset == DateTimeOffset.FromUnixTimeSeconds(2000000000) &&
            monthly.AdditionalWindows[0].Label == "30天（月周期）");
        Check("其他周期不误报窗口丢失", monthly.Error?.StartsWith("服务返回其他周期额度") == true);
        using var cli = JsonDocument.Parse("""{"rateLimits":{"primary":{"usedPercent":0,"windowDurationMins":43200,"resetsAt":2000000000},"secondary":null}}""");
        var mapped = CodexCliQuotaSource.Parse(cli.RootElement, "fixture", "free");
        Check("CLI月窗口正确转换分钟为秒并显示100%", mapped.AdditionalWindows is [ { Seconds: 2592000, Remaining: 100 } ]);
        using var shortWindow = JsonDocument.Parse("""{"rateLimits":{"primary":{"usedPercent":10,"windowDurationMins":15,"resetsAt":2000000000}}}""");
        Check("非标准短窗口按真实分钟展示", CodexCliQuotaSource.Parse(shortWindow.RootElement, "fixture", "free").AdditionalWindows[0].Label == "15分钟");
        return count;
    }
}
