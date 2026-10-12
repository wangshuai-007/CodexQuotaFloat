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
