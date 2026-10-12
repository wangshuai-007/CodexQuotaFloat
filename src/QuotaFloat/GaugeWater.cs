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
    private static readonly Brush WaterFiveInk = BrushOf("#206A9D");
    private static readonly Brush WaterWeekInk = BrushOf("#116C55");

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
            Text(dc, "5h", 8, 12, 8, WaterFiveInk);
            Text(dc, "7d", 24, 12, 8, WaterWeekInk);
        }
        else
        {
            var weekly = DisplayMode == QuotaDisplayMode.SevenDay;
            Text(dc, weekly ? "7d" : "5h", 16, 6, 8, weekly ? WaterWeekInk : WaterFiveInk);
            Text(dc, (weekly ? weekUnknown : fiveUnknown) ? "—" : $"{(weekly ? WeeklyLevel : Level):0}%",
                16, 18, 11, Ink, "Bahnschrift");
        }
    }

    private void DrawWater(DrawingContext dc)
    {
        if (DisplayMode == QuotaDisplayMode.Split)
        {
            DrawWaterPart(dc, Level, fiveUnknown, 0, 16, false);
            DrawWaterPart(dc, WeeklyLevel, weekUnknown, 16, 16, true);
            dc.DrawLine(new Pen(BrushOf("#FDFDFC"), 0.8), new Point(16, 43), new Point(16, 278));
        }
        else
        {
            var weekly = DisplayMode == QuotaDisplayMode.SevenDay;
            DrawWaterPart(dc, weekly ? WeeklyLevel : Level, weekly ? weekUnknown : fiveUnknown, 0, 32, weekly);
        }
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
        dc.DrawGeometry(new LinearGradientBrush(ColorOf(weekly ? "#C5E8D9" : "#C1E4F3"),
            ColorOf(weekly ? "#6AB899" : "#78ADD1"), 90) { Opacity = stale ? 0.28 : 0.8 }, null, water);
        DrawWaterSurface(dc, left, width, y, weekly);
        dc.Pop();
    }

    private void DrawWaterSurface(DrawingContext dc, double left, double width, double y, bool weekly)
    {
        var surface = new StreamGeometry();
        using (var path = surface.Open())
        {
            path.BeginFigure(new Point(left, y), false, false);
            path.BezierTo(new Point(left + width / 3, y - 2), new Point(left + width * 2 / 3, y + 2),
                new Point(left + width, y), true, false);
        }
        dc.PushOpacity(stale ? 0.3 : 0.9);
        dc.DrawGeometry(null, new Pen(Brushes.White, 2), surface);
        dc.DrawGeometry(null, new Pen(weekly ? WaterWeekInk : WaterFiveInk, 0.65), surface);
        dc.Pop();
    }
}
