using System.Windows;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class QuotaGauge
{
    private TrafficSnapshot traffic = TrafficSnapshot.Unknown();

    /// <summary>绘制最近60秒平均Token/s及趋势；无记录或未知保持破折号。</summary>
    public void SetTraffic(TrafficSnapshot value)
    {
        traffic = value;
        InvalidateVisual();
    }

    private void DrawTraffic(DrawingContext dc)
    {
        Text(dc, "均 tk/s", 16, 214, 8, Muted);
        Text(dc, traffic.AverageTokenRateLabel, 16, 230, traffic.AverageTokenRateLabel.Length > 4 ? 9 : 14, Ink, "Bahnschrift");
        var max = Math.Max(1, traffic.Buckets.DefaultIfEmpty(0).Max());
        for (var i = 0; i < 12; i++)
        {
            var height = i < traffic.Buckets.Count ? Math.Max(1, traffic.Buckets[i] / (double)max * 9) : 1;
            dc.DrawRectangle(traffic.Tokens is null ? Muted : Five, null, new Rect(5 + i * 1.9, 262 - height, 1.2, height));
        }
    }
}
