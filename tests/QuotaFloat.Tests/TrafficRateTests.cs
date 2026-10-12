using System.Text.Json;
using QuotaFloat.Core;

internal static class TrafficRateTests
{
    internal static int Run()
    {
        var passed = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name); passed++;
        }
        var now = DateTimeOffset.UtcNow;
        var stats = new TrafficAccumulator();
        string Record(string id, long tokens) => JsonSerializer.Serialize(new
            { request_id = id, timestamp = now, tokens = new { total_tokens = tokens } });
        TrafficSnapshot Read(DateTimeOffset at, params string[] records)
        {
            using var json = JsonDocument.Parse("[" + string.Join(",", records) + "]");
            stats.Ingest(json.RootElement, at); return stats.Snapshot(at);
        }
        var a = Record("a", 1000); var b = Record("b", 200); var c = Record("c", 400);
        Check("首帧历史用量不冒充Token/s", Read(now, a).TokensPerSecond is null);
        var second = Read(now.AddSeconds(1), a, b);
        Check("每秒新增Token而非60秒总量", second.TokensPerSecond == 200 && second.Tokens == 1200);
        Check("每秒新增请求数", second.RequestsPerSecond == 1 && second.SampleSeconds == 1);
        Check("重复快照速率为零不重复累计", Read(now.AddSeconds(2), a, b).TokensPerSecond == 0);
        var delayed = Read(now.AddSeconds(4), a, b, c);
        Check("慢采样使用真实间隔不是强制除1", delayed.TokensPerSecond == 200 && delayed.RequestsPerSecond == 0.5);
        Check("3秒内保留最近实际采样", delayed.ForDisplay(now.AddSeconds(7)).TokensPerSecond == 200);
        Check("超过3秒旧速率变未知不是假零", delayed.ForDisplay(now.AddSeconds(8)).TokensPerSecond is null);
        Check("旧数据保留截至时间不伪造更新时间", stats.Snapshot(now.AddSeconds(8)).UpdatedAt == now.AddSeconds(4));
        var rejected = false;
        try { using var bad = JsonDocument.Parse("[" + Record("rollback", 100) + ",{}]"); stats.Ingest(bad.RootElement, now.AddSeconds(5)); }
        catch (FormatException) { rejected = true; }
        Check("失败采样不推进基线和计数", rejected && Read(now.AddSeconds(6), a, b, c, Record("rollback", 100)).TokensPerSecond == 50);
        Check("零时间差不显示无限速率", Read(now.AddSeconds(6), a, b, c).TokensPerSecond is null);
        Check("时钟倒退重新建立采样基线", Read(now.AddSeconds(5), a, b, c).TokensPerSecond is null);
        stats.Clear();
        Check("切换来源重置速率基线", Read(now.AddSeconds(7), a).TokensPerSecond is null);
        Check("有效空快照下一秒明确零请求", Read(now.AddSeconds(8)).RequestsPerSecond == 0);
        Check("未接入速率未知", TrafficSnapshot.Unknown().TokenRateLabel == "—");
        Check("Token/s缩写带小数", (second with { TokensPerSecond = 1500 }).TokenRateLabel == "1.5k");
        return passed;
    }
}
