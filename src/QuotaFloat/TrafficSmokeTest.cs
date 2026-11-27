using System.Net;
using System.Net.Http;
using System.Text.Json;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckTrafficCadenceAsync(List<string> checks)
    {
        var handler = new RateFixtureHandler();
        var options = new AppSettings { UseDemo = false, RefreshSeconds = 60,
            ManagementUrl = "http://localhost:8317/v8/management", TrafficSnapshotUrl = "http://localhost:8317/stats/snapshot" };
        var fixture = new MainWindow(options, "fixture-key", false, handler);
        try
        {
            await Task.Delay(2600);
            Check(handler.Calls >= 2 && fixture.traffic.Snapshot.TokensPerSecond > 0,
                "真实1秒定时器至少两次GET取数并计算Token/s，非重绘旧值", checks);
            Check(fixture.session.UpdatedAt is null && fixture.refreshTimer.Interval == TimeSpan.FromSeconds(60),
                "1秒流量轮询不触发60秒额度轮询", checks);
            Check(fixture.details.TrafficLabel.Text.Contains("Token/s") && fixture.details.TrafficLabel.Text.Contains("最近60秒") &&
                !fixture.details.TrafficLabel.Text.Contains("请求/s") && !fixture.details.TrafficLabel.Text.Contains(" 次"),
                "详情仅显示Token速率和60秒Token累计，不显示请求次数", checks);
        }
        finally { fixture.Close(); }
        var calls = handler.Calls;
        await Task.Delay(1100);
        Check(handler.Calls == calls, "关闭后停止流量定时器及请求", checks);
        var slow = new RateFixtureHandler { Delay = 1200 };
        using var slowSession = new TrafficSession(options, "fixture-key", slow);
        var pending = slowSession.RefreshAsync();
        await slowSession.RefreshAsync();
        await pending;
        Check(slow.Calls == 1, "慢流量请求不重叠、不积压1秒查询", checks);
        await CheckKeeperCadenceAsync(checks);
    }

    private async Task CheckKeeperCadenceAsync(List<string> checks)
    {
        var handler = new KeeperRateFixtureHandler();
        const string address = "http://localhost:8123/";
        var fixture = new MainWindow(new AppSettings { UseDemo = false, UseKeeper = true, RefreshSeconds = 60 },
            "", false, createKeeper: () => new KeeperTrafficSource(address, "", false, handler));
        try
        {
            await fixture.traffic.RefreshAsync(true);
            var first = fixture.traffic.Snapshot.UpdatedAt!.Value;
            await Task.Delay(2200);
            Check(handler.EventCalls == 1 && fixture.trafficDisplayTimer.Interval == TimeSpan.FromSeconds(1) &&
                fixture.trafficTimer.Interval == TimeSpan.FromSeconds(5), "Keeper不随每秒界面更新发起请求", checks);
            Check(fixture.traffic.SnapshotForDisplay(first.AddSeconds(5)).AverageTokensPerSecond > 0 &&
                fixture.traffic.SnapshotForDisplay(first.AddSeconds(11)).AverageTokensPerSecond is null,
                "Keeper正常采样间隔不误报过期，旧快照仍会停止显示速率", checks);
            await handler.SecondPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(handler.EventCalls == 2, "Keeper五秒自动轮询仍会取数", checks);
        }
        finally { fixture.Close(); }
    }

    private sealed class KeeperRateFixtureHandler : HttpMessageHandler
    {
        internal int EventCalls;
        internal readonly TaskCompletionSource SecondPoll = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method != HttpMethod.Get) throw new InvalidOperationException("Keeper测试只能读取");
            var payload = request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/auth/session" => """{"authenticated":true}""",
                "/api/v1/usage/events" => ReadEvents(),
                _ => throw new InvalidOperationException("Keeper测试访问了非预期路径")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) });
        }

        private string ReadEvents()
        {
            if (++EventCalls == 2) SecondPoll.SetResult();
            return JsonSerializer.Serialize(new { events = new[] { new { id = "fixture-keeper",
                timestamp = DateTimeOffset.UtcNow.AddSeconds(-1), tokens = new { total_tokens = 200 } } }, has_more = false });
        }
    }

    private sealed class RateFixtureHandler : HttpMessageHandler
    {
        internal int Calls;
        internal int Delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != "/stats/snapshot")
                throw new InvalidOperationException("流量定时器访问了非只读快照");
            var count = ++Calls;
            if (Delay > 0) await Task.Delay(Delay, token);
            var records = Enumerable.Range(1, count).Select(i => new { request_id = "fixture-" + i,
                timestamp = DateTimeOffset.Now, tokens = new { total_tokens = 200 } });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { records })) };
        }
    }
}
