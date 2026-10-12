using System.Net;
using System.Net.Http;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>独立流量刷新，额度查询耗时不阻塞流量；只读快照，无消费队列请求。</summary>
internal sealed class TrafficSession : IDisposable
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly TrafficAccumulator accumulator = new();
    private CancellationTokenSource cancellation = new();
    private AppSettings settings;
    private string key;
    private int version;
    private bool loading;
    private bool paused;
    internal TrafficSnapshot Snapshot { get; private set; } = TrafficSnapshot.Unknown();
    internal event Action? Changed;

    internal TrafficSession(AppSettings options, string managementKey) { settings = options; key = managementKey; }

    internal void Configure(AppSettings options, string managementKey)
    {
        version++;
        cancellation.Cancel();
        cancellation.Dispose();
        cancellation = new();
        settings = options;
        key = managementKey;
        loading = false;
        paused = false;
        accumulator.Clear();
        Snapshot = TrafficSnapshot.Unknown();
        Changed?.Invoke();
    }

    internal async Task RefreshAsync(bool manual = false)
    {
        if (loading || (paused && !manual)) return;
        loading = true;
        var current = version;
        var token = cancellation.Token;
        try
        {
            var now = DateTimeOffset.Now;
            if (settings.UseDemo) Snapshot = Demo(now);
            else if (settings.UseLocalAuth) Snapshot = TrafficSnapshot.Unknown("本地认证仅查询额度，不提供最近60秒Token/请求统计");
            else
            {
                using var data = await new CpaTrafficSource(http, settings.ManagementUrl, key, settings.TrafficSnapshotUrl).ReadAsync(token);
                if (current != version) return;
                accumulator.Ingest(data.RootElement, now);
                Snapshot = accumulator.Snapshot(now);
            }
            paused = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception error) when (error is HttpRequestException or ArgumentException or
            System.Text.Json.JsonException or FormatException or OperationCanceledException or InvalidOperationException)
        {
            if (current != version) return;
            paused = error is ArgumentException || error is HttpRequestException
                { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound };
            var message = error is ArgumentException ? error.Message : CpaQuotaSource.SafeError(error);
            Snapshot = TrafficSnapshot.Unknown(message);
        }
        finally
        {
            if (current == version) { loading = false; Changed?.Invoke(); }
        }
    }

    private static TrafficSnapshot Demo(DateTimeOffset now)
    {
        var phase = (int)(now.ToUnixTimeSeconds() / 5 % 12);
        long[] values = [0, 320, 170, 650, 120, 0, 930, 440, 170, 780, 220, 510];
        var buckets = Enumerable.Range(0, 12).Select(i => values[(i + phase) % 12]).ToArray();
        return new(buckets.Sum(), 18, buckets, now);
    }

    public void Dispose() { version++; cancellation.Cancel(); cancellation.Dispose(); http.Dispose(); }
}
