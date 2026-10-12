using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class QuotaGauge
{
    public static readonly DependencyProperty WeeklyLevelProperty = DependencyProperty.Register(
        nameof(WeeklyLevel), typeof(double), typeof(QuotaGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double WeeklyLevel { get => (double)GetValue(WeeklyLevelProperty); set => SetValue(WeeklyLevelProperty, value); }
    /// <summary>显示方式：0=5小时；1=7天；2=左5小时、右7天。</summary>
    public QuotaDisplayMode DisplayMode { get; set; }
    private bool fiveUnknown = true;
    private bool weekUnknown = true;

    private void UpdateWater(IReadOnlyList<AccountQuota> values, DateTimeOffset time, bool animate)
    {
        var five = QuotaMath.WindowWaterLevel(values, time, QuotaDisplayMode.FiveHour);
        var week = QuotaMath.WindowWaterLevel(values, time, QuotaDisplayMode.SevenDay);
        fiveUnknown = five is null;
        weekUnknown = week is null;
        AnimateLevel(LevelProperty, five ?? 0, animate);
        AnimateLevel(WeeklyLevelProperty, week ?? 0, animate);
        var fiveText = five is { } f ? $"{f:0}%" : "未知";
        var weekText = week is { } w ? $"{w:0}%" : "未知";
        System.Windows.Automation.AutomationProperties.SetHelpText(this,
            $"全部有效账号等权平均 · 5h {fiveText} · 7天 {weekText}" + (stale ? " · 旧数据" : ""));
    }

    private void AnimateLevel(DependencyProperty property, double target, bool animate)
    {
        if (animate && SystemParameters.ClientAreaAnimation)
            BeginAnimation(property, new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)));
        else { BeginAnimation(property, null); SetValue(property, target); }
    }

    private void DrawWaterLabels(DrawingContext dc)
    {
        if (DisplayMode == QuotaDisplayMode.Split)
        {
            Text(dc, "5h " + (fiveUnknown ? "—" : $"{Level:0}%"), 16, 8, Level >= 99.5 ? 8 : 9, Five, "Bahnschrift");
            Text(dc, "7d " + (weekUnknown ? "—" : $"{WeeklyLevel:0}%"), 16, 22, WeeklyLevel >= 99.5 ? 8 : 9, Week, "Bahnschrift");
        }
        else
        {
            var weekly = DisplayMode == QuotaDisplayMode.SevenDay;
            Text(dc, weekly ? "7d" : "5h", 16, 6, 8, weekly ? Week : Five);
            Text(dc, (weekly ? weekUnknown : fiveUnknown) ? "—" : $"{(weekly ? WeeklyLevel : Level):0}%",
                16, 18, 11, Ink, "Bahnschrift");
        }
    }

    private void DrawWater(DrawingContext dc)
    {
        if (DisplayMode == QuotaDisplayMode.Split)
        {
            DrawSplitWater(dc);
        }
        else
        {
            var weekly = DisplayMode == QuotaDisplayMode.SevenDay;
            DrawWaterPart(dc, weekly ? WeeklyLevel : Level, weekly ? weekUnknown : fiveUnknown, 0, 32, weekly);
        }
    }

    private void DrawSplitWater(DrawingContext dc)
    {
        const double top = 43, bottom = 204;
        dc.DrawRectangle(BrushOf("#E9F3FB"), null, new Rect(1, top, 14, bottom - top));
        dc.DrawRectangle(BrushOf("#E6F5ED"), null, new Rect(17, top, 14, bottom - top));
        DrawSplitPart(dc, Level, fiveUnknown, 1, top, bottom, Five, "#77B8E1");
        DrawSplitPart(dc, WeeklyLevel, weekUnknown, 17, top, bottom, Week, "#72C7A7");
        dc.DrawRectangle(BrushOf("#FDFDFC"), null, new Rect(15, 42, 2, bottom - 42));
        dc.DrawLine(new Pen(Five, 2), new Point(2, top), new Point(14, top));
        dc.DrawLine(new Pen(Week, 2), new Point(18, top), new Point(30, top));
    }

    private void DrawSplitPart(DrawingContext dc, double level, bool unknown, double left,
        double top, double bottom, Brush color, string light)
    {
        if (unknown) return;
        var y = bottom - Math.Clamp(level, 0, 100) / 100 * (bottom - top);
        var gradient = new LinearGradientBrush(ColorOf(light), ((SolidColorBrush)color).Color, 90)
            { Opacity = stale ? 0.28 : 0.82 };
        dc.DrawRectangle(gradient, null, new Rect(left, y, 14, bottom - y));
        dc.DrawLine(new Pen(color, 1.5),
            new Point(left + 1, y), new Point(left + 13, y));
    }

    private void DrawWaterPart(DrawingContext dc, double level, bool unknown, double left, double width, bool weekly)
    {
        if (unknown) return;
        var y = 278 - Math.Clamp(level, 0, 100) / 100 * 236;
        var water = new StreamGeometry();
        using (var path = water.Open())
        {
            path.BeginFigure(new Point(left, y), true, true);
            path.BezierTo(new Point(left + width / 3, y - 2), new Point(left + width * 2 / 3, y + 2),
                new Point(left + width, y), true, false);
            path.LineTo(new Point(left + width, 281), true, false);
            path.LineTo(new Point(left, 281), true, false);
        }
        dc.PushClip(new RectangleGeometry(new Rect(left, 42, width, 239)));
        dc.DrawGeometry(new LinearGradientBrush(ColorOf(weekly ? "#CCEBDD" : "#C5E3F2"),
            ColorOf(weekly ? "#76C5A5" : "#79B7DC"), 90) { Opacity = stale ? 0.28 : 0.72 }, null, water);
        dc.Pop();
    }
}
