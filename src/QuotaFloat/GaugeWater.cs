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
    public static readonly DependencyProperty MonthlyLevelProperty = DependencyProperty.Register(
        nameof(MonthlyLevel), typeof(double), typeof(QuotaGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double MonthlyLevel { get => (double)GetValue(MonthlyLevelProperty); set => SetValue(MonthlyLevelProperty, value); }
    /// <summary>显示方式：0=5小时；1=7天；2=左5小时、右7天。</summary>
    public QuotaDisplayMode DisplayMode { get; set; }
    private IReadOnlyList<WaterColumn> waterColumns = [];
    internal IReadOnlyList<WaterColumn> VisibleWaterColumns => waterColumns;
    private static readonly Brush WaterFiveInk = BrushOf("#206A9D");
    private static readonly Brush WaterWeekInk = BrushOf("#116C55");
    private static readonly Brush WaterMonthInk = BrushOf("#995019");

    private void UpdateWater(IReadOnlyList<AccountQuota> values, DateTimeOffset time, bool animate)
    {
        var five = QuotaMath.WindowWaterLevel(values, time, QuotaDisplayMode.FiveHour);
        var week = QuotaMath.WindowWaterLevel(values, time, QuotaDisplayMode.SevenDay);
        waterColumns = WaterColumns.Build(values, time, DisplayMode);
        var month = waterColumns.FirstOrDefault(column => column.Id == "m")?.Remaining;
        AnimateLevel(LevelProperty, five ?? 0, animate);
        AnimateLevel(WeeklyLevelProperty, week ?? 0, animate);
        AnimateLevel(MonthlyLevelProperty, month ?? 0, animate);
        System.Windows.Automation.AutomationProperties.SetHelpText(this,
            "全部有效账号按周期等权平均 · " + string.Join(" · ", waterColumns.Select(column =>
                column.Id + " " + (column.Remaining is { } remaining ? $"{remaining:0}%" : "未知"))) + (stale ? " · 旧数据" : ""));
    }

    private void AnimateLevel(DependencyProperty property, double target, bool animate)
    {
        if (animate && SystemParameters.ClientAreaAnimation)
            BeginAnimation(property, new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)));
        else { BeginAnimation(property, null); SetValue(property, target); }
    }

    private void DrawWaterLabels(DrawingContext dc)
    {
        if (waterColumns.Count > 1)
        {
            for (var i = 0; i < waterColumns.Count; i++)
                Text(dc, waterColumns[i].Id, (i + 0.5) * 32 / waterColumns.Count, 12,
                    waterColumns.Count == 3 ? 7 : 8, WaterInk(waterColumns[i].Id));
        }
        else if (waterColumns.Count == 1)
        {
            var column = waterColumns[0];
            Text(dc, column.Id, 16, 6, 8, WaterInk(column.Id));
            Text(dc, column.Remaining is null ? "—" : $"{ColumnLevel(column.Id):0}%",
                16, 18, 11, Ink, "Bahnschrift");
        }
    }

    private void DrawWater(DrawingContext dc)
    {
        for (var i = 0; i < waterColumns.Count; i++)
        {
            var column = waterColumns[i];
            var width = 32d / waterColumns.Count;
            DrawWaterPart(dc, ColumnLevel(column.Id), column.Remaining is null, i * width, width, column.Id);
            if (i > 0) dc.DrawLine(new Pen(BrushOf("#FDFDFC"), 0.8),
                new Point(i * width, 43), new Point(i * width, 278));
        }
    }

    private double ColumnLevel(string id) => id == "m" ? MonthlyLevel : id == "7d" ? WeeklyLevel : Level;

    private static Brush WaterInk(string id) => id == "m" ? WaterMonthInk : id == "7d" ? WaterWeekInk : WaterFiveInk;

    private void DrawWaterPart(DrawingContext dc, double level, bool unknown, double left, double width, string id)
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
        dc.DrawGeometry(new LinearGradientBrush(ColorOf(id == "m" ? "#F4DDC2" : id == "7d" ? "#C5E8D9" : "#C1E4F3"),
            ColorOf(id == "m" ? "#D5A06B" : id == "7d" ? "#6AB899" : "#78ADD1"), 90) { Opacity = stale ? 0.28 : 0.8 }, null, water);
        DrawWaterSurface(dc, left, width, y, id);
        dc.Pop();
    }

    private void DrawWaterSurface(DrawingContext dc, double left, double width, double y, string id)
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
        dc.DrawGeometry(null, new Pen(WaterInk(id), 0.65), surface);
        dc.Pop();
    }
}
