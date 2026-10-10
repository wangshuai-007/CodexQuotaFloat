using System.Net;
using System.Text.Json;
using QuotaFloat.Core;

internal static class TrafficOrderingTests
{
    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(string name, bool ok)
        {
            if (!ok) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name); passed++;
        }
        var now = DateTimeOffset.UtcNow;
        await CheckOrderingAsync(Check);
        CheckAccumulator(now, Check);
        await CheckSourceAsync(Check);
        return passed;
    }

    private static async Task CheckOrderingAsync(Action<string, bool> Check)
    {
        var accounts = await new DemoQuotaSource(3).ReadAsync(default);
        var order = AccountOrdering.Move(accounts, ["missing"], accounts[2].Id, 0);
        Check("本地置顶", AccountOrdering.Apply(accounts, order)[0].Id == accounts[2].Id);
        order = AccountOrdering.Move(accounts, order, accounts[2].Id, int.MaxValue);
        Check("本地最后", AccountOrdering.Apply(accounts, order)[2].Id == accounts[2].Id);
        order = AccountOrdering.Move(accounts, order, accounts[2].Id, 1);
        Check("本地拖动目标位置", AccountOrdering.Apply(accounts, order)[1].Id == accounts[2].Id);
        Check("保留暂时不在列表中的账号偏好", order.Contains("missing"));
        Check("来源顺序未改变", accounts[0].Id == "demo-01");
        Check("新账号按来源顺序追加", AccountOrdering.Apply(accounts, [accounts[1].Id])[2].Id == accounts[2].Id);
        Check("重复排序ID不重复账号", AccountOrdering.Apply(accounts, [accounts[0].Id, accounts[0].Id]).Count == 3);
        Check("排序设置JSON往返", JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(order))!.SequenceEqual(order));
        Check("允许5秒", RefreshInterval.IsValid(5));
        Check("拒绝4秒", !RefreshInterval.IsValid(4));
        Check("加载过小设置夹紧5秒", RefreshInterval.Clamp(-1) == 5);
        Check("刷新间隔上限", RefreshInterval.IsValid(3600) && !RefreshInterval.IsValid(3601));
        Check("未知流量不是零", TrafficSnapshot.Unknown().Tokens is null);
    }

    private static void CheckAccumulator(DateTimeOffset now, Action<string, bool> Check)
    {
        var stats = new TrafficAccumulator();
        string Record(string id, int age, long tokens) => JsonSerializer.Serialize(new
            { request_id = id, timestamp = now.AddSeconds(-age), tokens = new { total_tokens = tokens } });
        void Ingest(string json) { using var data = JsonDocument.Parse(json); stats.Ingest(data.RootElement, now); }
        var snapshot = "{\"records\":[" + Record("a", 1, 100) + "," + Record("b", 59, 200) + "," + Record("old", 60, 999) + "]}";
        Ingest(snapshot);
        Check("只统计最近60秒", stats.Snapshot(now).Tokens == 300 && stats.Snapshot(now).Requests == 2);
        Check("12格5秒趋势", stats.Snapshot(now).Buckets.Count == 12 && stats.Snapshot(now).Buckets[11] == 100 && stats.Snapshot(now).Buckets[0] == 200);
        Ingest(snapshot);
        Check("重复快照不重复计算", stats.Snapshot(now).Requests == 2);
        foreach (var invalid in new[] { "{}", "{\"usage\":null}", "{\"usage\":{\"total_tokens\":123}}", "{\"records\":[{}]}", "{\"records\":[" + Record("bad", 0, -1) + "]}" })
        {
            try { Ingest(invalid); Check("错误快照拒绝", false); }
            catch (FormatException) { Check("错误快照明确拒绝且不污染窗口", stats.Snapshot(now).Tokens == 300); }
        }
        try { Ingest("{\"records\":[" + Record("partial", 0, 111) + ",{}]}"); }
        catch (FormatException) { Check("失败解析原子回滚", stats.Snapshot(now).Requests == 2); }
        stats.Clear();
        Ingest("{\"usage\":{\"apis\":{\"anonymous\":{\"models\":{\"codex\":{\"details\":[" + Record("legacy", 2, 400) + "]}}}}}}");
        Check("旧版只读usage明细解析", stats.Snapshot(now).Tokens == 400);
        Check("过期记录自动清除", stats.Snapshot(now.AddSeconds(61)).Requests == 0);
        stats.Clear(); Ingest("{\"records\":[]}");
        Check("有效空快照明确为零", stats.Snapshot(now).Tokens == 0);
        stats.Clear();
        var noId = JsonSerializer.Serialize(new { timestamp = now, tokens = new { total_tokens = 10 } });
        Ingest("[" + noId + "," + noId + "]"); Ingest("[" + noId + "," + noId + "]");
        Check("旧快照同内容不同请求保留数量且重复轮询不累加", stats.Snapshot(now).Requests == 2);
    }

    private static async Task CheckSourceAsync(Action<string, bool> Check)
    {
        Check("旧版只读地址", CpaTrafficSource.ResolveUrl("http://localhost:8317/v0/management", "").AbsolutePath == "/v0/management/usage");
        foreach (var url in new[] { "", "http://evil.test/usage", "http://localhost:8317/usage-queue", "http://localhost:8317/observability/usage/queue", "http://localhost:8317/usage?key=x", "http://localhost:8317/%71ueue" })
        {
            try { CpaTrafficSource.ResolveUrl("http://localhost:8317/v8/management", url); Check("拒绝不安全或不可用流量源", false); }
            catch (ArgumentException) { Check("拒绝队列/跨源/密钥参数/无快照v8", true); }
        }
        using var handler = new SnapshotHandler();
        using var http = new HttpClient(handler);
        using var response = await new CpaTrafficSource(http, "http://localhost:8317/v8/management", "test-key", "http://localhost:8317/stats/snapshot").ReadAsync(default);
        Check("只发送GET快照请求，无队列请求", handler.Path == "/stats/snapshot" && handler.Method == HttpMethod.Get);
        Check("只读快照使用管理鉴权头", handler.Key == "test-key");
    }

    private sealed class SnapshotHandler : HttpMessageHandler
    {
        internal string? Path, Key;
        internal HttpMethod? Method;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Path = request.RequestUri!.AbsolutePath; Method = request.Method; Key = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"records\":[]}") });
        }
    }
}
