using System.Windows;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class QuotaGauge
{
    private TrafficSnapshot traffic = TrafficSnapshot.Unknown();

    /// <summary>绘制最近60秒Token、请求次数和12格趋势；未知保持破折号。</summary>
    public void SetTraffic(TrafficSnapshot value)
    {
        traffic = value;
        InvalidateVisual();
    }

    private void DrawTraffic(DrawingContext dc)
    {
        Text(dc, "tk/60s", 16, 215, 7, Muted);
        Text(dc, traffic.TokenLabel, 16, 225, 10, Ink, "Bahnschrift");
        var requests = traffic.Requests is { } count ? count >= 1000 ? $"{count / 1000d:0.#}k次" : $"{count}次" : "—次";
        Text(dc, requests, 16, 239, 9, Ink);
        var max = Math.Max(1, traffic.Buckets.DefaultIfEmpty(0).Max());
        for (var i = 0; i < 12; i++)
        {
            var height = i < traffic.Buckets.Count ? Math.Max(1, traffic.Buckets[i] / (double)max * 9) : 1;
            dc.DrawRectangle(traffic.Tokens is null ? Muted : Five, null, new Rect(5 + i * 1.9, 262 - height, 1.2, height));
        }
    }
}
