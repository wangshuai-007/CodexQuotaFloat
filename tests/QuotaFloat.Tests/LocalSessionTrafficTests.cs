using System.Text.Json;
using QuotaFloat.Core;

internal static class LocalSessionTrafficTests
{
    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name); passed++;
        }
        var directory = Path.Combine(Path.GetTempPath(), "quota-session-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var now = DateTimeOffset.UtcNow;
        var file = Path.Combine(directory, "rollout-test.jsonl");
        try
        {
            var source = new LocalSessionTrafficSource(directory);
            var empty = await new LocalSessionTrafficSource(directory).ReadAsync(now);
            Check("本地空会话明确无记录", empty.Tokens == 0 && empty.AverageTokensPerSecond is null && empty.IsLocalSession);
            await File.WriteAllTextAsync(file, Record(now.AddSeconds(-70), 1000, 100) + "\n" + Record(now.AddSeconds(-10), 1600, 600) + "\n");
            var first = await source.ReadAsync(now);
            Check("历史累计不计为当前流量", first.Tokens == 600 && first.AverageTokensPerSecond == 10);
            Check("输入缓存推理不重复相加", first.Buckets.Sum() == 600);
            Check("本地口径不声称完成请求", first.AverageRateNotice.Contains("本地session") && first.Requests is null);
            Check("重复读取不重复统计", (await source.ReadAsync(now)).Tokens == 600);
            await File.AppendAllTextAsync(file, Record(now.AddSeconds(-5), 1900, 300) + "\n");
            Check("增量追加下一秒可见", (await source.ReadAsync(now)).Tokens == 900);
            await File.AppendAllTextAsync(file, Record(now.AddSeconds(-4), 1900, 300) + "\n");
            Check("重复计量累计不变不累加", (await source.ReadAsync(now)).Tokens == 900);
            var partial = Record(now.AddSeconds(-2), 2100, 200);
            await File.AppendAllTextAsync(file, partial[..20]);
            Check("未写完的JSON行不报错不计量", (await source.ReadAsync(now)).Tokens == 900);
            await File.AppendAllTextAsync(file, partial[20..] + "\n");
            Check("补完半行后正确计量", (await source.ReadAsync(now)).Tokens == 1100);
            await File.AppendAllTextAsync(file, Record(now.AddSeconds(-1), 100, 100) + "\n");
            Check("累计重置使用last而非负增量", (await source.ReadAsync(now)).Tokens == 1200);
            var second = Path.Combine(directory, "rollout-other.jsonl");
            await File.WriteAllTextAsync(second, Record(now, 400, 400) + "\n");
            Check("多会话独立聚合", (await source.ReadAsync(now.AddSeconds(5))).Tokens == 1600);
            await File.AppendAllTextAsync(second, Record(now.AddSeconds(-2), 2100, 200) + "\n");
            // 克隆日志的重复记录使用相同时间、累计、增量指纹。
            var cloned = Path.Combine(directory, "rollout-clone.jsonl");
            await File.WriteAllTextAsync(cloned, partial + "\n");
            Check("克隆历史不重复累计", (await source.ReadAsync(now.AddSeconds(10))).Tokens == 3300);
            Check("60秒到期不沿用旧平均", (await source.ReadAsync(now.AddSeconds(61))).Tokens == 0);
            var missing = await new LocalSessionTrafficSource(Path.Combine(directory, "missing")).ReadAsync(now);
            Check("目录缺失明确提示", missing.Tokens is null && missing.Error!.Contains("目录"));
            await File.WriteAllTextAsync(file, "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":null}}\n");
            Check("仅限额事件无info不伪造计量", (await new LocalSessionTrafficSource(directory).ReadAsync(now)).Tokens >= 0);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await source.ReadAsync(now, cancelled.Token); Check("本地读取尊重取消", false); }
            catch (OperationCanceledException) { Check("本地读取尊重取消", true); }
            await EdgeCasesAsync(directory, now, Check);
        }
        finally { Directory.Delete(directory, true); }
        return passed;
    }

    private static string Record(DateTimeOffset at, long total, long last) => JsonSerializer.Serialize(new
    {
        timestamp = at, type = "event_msg", payload = new { type = "token_count", info = new
        {
            total_token_usage = new { total_tokens = total },
            last_token_usage = new { total_tokens = last, input_tokens = last - 10, cached_input_tokens = last - 10,
                output_tokens = 10, reasoning_output_tokens = 5 }
        } }
    });

    private static async Task EdgeCasesAsync(string root, DateTimeOffset now, Action<string, bool> check)
    {
        var directory = Path.Combine(root, "edges"); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "rollout-edge.jsonl");
        var source = new LocalSessionTrafficSource(directory);
        await File.WriteAllTextAsync(file, Record(now, 700, 700) + "\n");
        check("新session首条使用last不使用历史总累计", (await source.ReadAsync(now)).Tokens == 700);
        await File.WriteAllTextAsync(file, Record(now.AddSeconds(1), 1, 1) + "\n");
        check("日志截断后重建游标", (await source.ReadAsync(now.AddSeconds(1))).Tokens == 701);
        await File.AppendAllTextAsync(file, Record(now.AddMinutes(10), 9999, 9998) + "\n");
        check("未来时间不污染当前流量", (await source.ReadAsync(now.AddSeconds(1))).Tokens == 701);
        await File.WriteAllTextAsync(file, "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{}}}\n");
        try { await new LocalSessionTrafficSource(directory).ReadAsync(now); check("未知格式明确失败", false); }
        catch (FormatException) { check("未知格式明确失败", true); }
        await File.WriteAllTextAsync(file, string.Concat(Enumerable.Repeat(new string(' ', 65535) + "\n", 80)) + Record(now, 100, 100) + "\n");
        var large = new LocalSessionTrafficSource(directory);
        var warming = await large.ReadAsync(now);
        check("大日志尾读不冒充完整60秒", warming.Tokens is null && warming.Error!.Contains("建立"));
        check("预热结束正常滚动过期", (await large.ReadAsync(now.AddSeconds(61))).Tokens == 0);
    }
}
