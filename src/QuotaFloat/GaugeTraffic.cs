using System.Windows;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class QuotaGauge
{
    private TrafficSnapshot traffic = TrafficSnapshot.Unknown();

    /// <summary>绘制Token/s、请求/s和最近60秒趋势；未知保持破折号。</summary>
    public void SetTraffic(TrafficSnapshot value)
    {
        traffic = value;
        InvalidateVisual();
    }

    private void DrawTraffic(DrawingContext dc)
    {
        Text(dc, "tk/s", 16, 214, 8, Muted);
        Text(dc, traffic.TokenRateLabel, 16, 225, traffic.TokenRateLabel.Length > 4 ? 9 : 12, Ink, "Bahnschrift");
        Text(dc, $"{traffic.RequestRateLabel}次/s", 16, 242, 7, Ink, "Bahnschrift");
        var max = Math.Max(1, traffic.Buckets.DefaultIfEmpty(0).Max());
        for (var i = 0; i < 12; i++)
        {
            var height = i < traffic.Buckets.Count ? Math.Max(1, traffic.Buckets[i] / (double)max * 9) : 1;
            dc.DrawRectangle(traffic.Tokens is null ? Muted : Five, null, new Rect(5 + i * 1.9, 262 - height, 1.2, height));
        }
    }
}
