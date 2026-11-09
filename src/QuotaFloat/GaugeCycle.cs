using System.Windows;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class QuotaGauge
{
    private static readonly Point[] CycleOutline = CreateCycleOutline();

    private void DrawCycleProgress(DrawingContext dc)
    {
        if (stale || ResetCycleProgress.Calculate(accounts, now) is not { } percent || percent <= 0) return;
        var remaining = (496 + 30 * Math.PI) * percent / 100;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(CycleOutline[0], false, false);
            for (var i = 1; i < CycleOutline.Length && remaining > 0; i++)
            {
                var delta = CycleOutline[i] - CycleOutline[i - 1];
                var length = delta.Length;
                path.LineTo(CycleOutline[i - 1] + delta * Math.Min(1, remaining / length), true, false);
                remaining -= length;
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(Cycle, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geometry);
    }

    private static Point[] CreateCycleOutline()
    {
        var points = new List<Point>();
        // 从顶部中心顺时针沿胶囊外沿绘制，保持原水位和交互区域。
        void Arc(double centerY, double start, double end)
        {
            for (var degree = start; degree <= end; degree += 3)
            {
                var angle = degree * Math.PI / 180;
                points.Add(new Point(16 + 15 * Math.Cos(angle), centerY + 15 * Math.Sin(angle)));
            }
        }
        Arc(16, -90, 0);
        points.Add(new Point(31, 264));
        Arc(264, 0, 180);
        points.Add(new Point(1, 16));
        Arc(16, 180, 270);
        return points.ToArray();
    }
}
