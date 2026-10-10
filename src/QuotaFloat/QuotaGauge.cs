using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>原生矢量水柱；水位是有效账号的双窗口剩余下限平均值。</summary>
public sealed class QuotaGauge : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(QuotaGauge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    private IReadOnlyList<AccountQuota> accounts = [];
    private DateTimeOffset now;
    private string source = "演示";
    private bool unknown = true;
    private bool stale;
    public event Action? InvokeRequested;
    private static readonly Brush Ink = BrushOf("#203038");
    private static readonly Brush Muted = BrushOf("#536D76");
    private static readonly Brush Five = BrushOf("#287CBA");
    private static readonly Brush Week = BrushOf("#148568");
    private static readonly Brush Cycle = BrushOf("#B76621");

    public QuotaGauge()
    {
        Width = 96;
        Height = 304;
        Focusable = true;
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    /// <summary>更新快照，只有平台允许动效时执行短水位过渡。</summary>
    public void Update(IReadOnlyList<AccountQuota> values, string label, DateTimeOffset time, bool oldData, bool animate)
    {
        accounts = values;
        now = time;
        source = label;
        stale = oldData;
        var value = QuotaMath.WaterLevel(values, time);
        unknown = value is null;
        var target = value ?? 0;
        if (animate && SystemParameters.ClientAreaAnimation)
            BeginAnimation(LevelProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)) { FillBehavior = FillBehavior.HoldEnd });
        else
        {
            BeginAnimation(LevelProperty, null);
            Level = target;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var capsule = new Rect(2, 2, 92, 300);
        var background = new LinearGradientBrush(ColorOf("#FFFFFF"), ColorOf("#E9F1F0"), 90);
        dc.DrawRoundedRectangle(background, new Pen(BrushOf("#CEDDDF"), 1), capsule, 43, 43);
        dc.PushClip(new RectangleGeometry(capsule, 43, 43));
        DrawWater(dc);
        DrawTicks(dc);
        Text(dc, "CODEX", 48, 26, 11, Muted, true);
        Text(dc, unknown ? "—" : $"{Level:0}", 46, 47, 34, Ink, true, "Bahnschrift");
        if (!unknown) Text(dc, "%", 73, 63, 12, Muted, true);
        Text(dc, stale ? "旧数据" : "可用水位", 48, 88, 11, Muted, true);
        DrawMeters(dc);
        Text(dc, $"{accounts.Count} 个账号", 48, 240, 12, Ink, true);
        Text(dc, source, 48, 260, 11, Muted, true);
        dc.DrawRoundedRectangle(BrushOf("#6C939F"), null, new Rect(36, 284, 24, 3), 1.5, 1.5);
        dc.Pop();
        if (IsKeyboardFocused)
            dc.DrawRoundedRectangle(null, new Pen(Five, 2), new Rect(1, 1, 94, 302), 44, 44);
    }

    private void DrawWater(DrawingContext dc)
    {
        var y = 294 - Math.Clamp(Level, 0, 100) / 100 * 268;
        var fill = new LinearGradientBrush(ColorOf("#C5E9E8"), ColorOf("#69BCC6"), 90) { Opacity = stale ? 0.28 : 0.72 };
        var water = new StreamGeometry();
        using (var path = water.Open())
        {
            path.BeginFigure(new Point(0, y), true, true);
            path.BezierTo(new Point(26, y - 9), new Point(57, y + 10), new Point(96, y), true, false);
            path.LineTo(new Point(96, 306), true, false);
            path.LineTo(new Point(0, 306), true, false);
        }
        dc.DrawGeometry(fill, null, water);
        var surface = new StreamGeometry();
        using (var path = surface.Open())
        {
            path.BeginFigure(new Point(0, y + 4), false, false);
            path.BezierTo(new Point(38, y + 12), new Point(65, y - 5), new Point(96, y + 4), true, false);
        }
        dc.DrawGeometry(null, new Pen(BrushOf("#83C9D0"), 1), surface);
    }

    private static void DrawTicks(DrawingContext dc)
    {
        for (var i = 0; i < 15; i++)
        {
            var y = 52 + i * 14;
            var length = i % 5 == 0 ? 7 : 3;
            dc.DrawLine(new Pen(BrushOf("#AABFC2"), 0.8), new Point(8, y), new Point(8 + length, y));
            dc.DrawLine(new Pen(BrushOf("#AABFC2"), 0.8), new Point(88 - length, y), new Point(88, y));
        }
    }

    private void DrawMeters(DrawingContext dc)
    {
        if (accounts.Count <= 4)
        {
            for (var i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                var y = 124 + i * 24;
                Text(dc, $"{i + 1:00}", 16, y - 1, 8, Muted, true);
                Meter(dc, account.FiveHourRemaining, Five, y);
                Meter(dc, account.WeeklyRemaining, Week, y + 5);
                Meter(dc, QuotaMath.WeekElapsed(account.WeeklyReset, now), Cycle, y + 10);
            }
            if (accounts.Count == 0) Text(dc, "暂无数据", 48, 153, 11, Muted, true);
            return;
        }
        var usable = accounts.Where(a => a.Available(now) is not null).ToArray();
        Meter(dc, usable.Length == 0 ? null : usable.Average(a => a.FiveHourRemaining), Five, 137);
        Meter(dc, usable.Length == 0 ? null : usable.Average(a => a.WeeklyRemaining), Week, 157);
        Meter(dc, usable.Length == 0 ? null : usable.Average(a => QuotaMath.WeekElapsed(a.WeeklyReset, now)), Cycle, 177);
        Text(dc, "均值", 48, 200, 10, Muted, true);
    }

    private static void Meter(DrawingContext dc, double? value, Brush color, double y)
    {
        dc.DrawRoundedRectangle(BrushOf("#C9DADA"), null, new Rect(29, y, 46, 2.5), 1.25, 1.25);
        if (value is { } percent)
            dc.DrawRoundedRectangle(color, null, new Rect(29, y, 46 * QuotaMath.Clamp(percent) / 100, 2.5), 1.25, 1.25);
    }

    private void Text(DrawingContext dc, string text, double x, double y, double size, Brush color, bool centered, string font = "Segoe UI Variable Text")
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(font), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(centered ? x - formatted.Width / 2 : x, y));
    }

    private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Brush BrushOf(string hex) => new SolidColorBrush(ColorOf(hex));
    protected override AutomationPeer OnCreateAutomationPeer() => new GaugePeer(this);

    private sealed class GaugePeer(QuotaGauge gauge) : FrameworkElementAutomationPeer(gauge), IInvokeProvider
    {
        protected override string GetNameCore() => $"Codex 额度浮窗，{gauge.source}，{(gauge.unknown ? "未知" : $"{gauge.Level:0}%")}，{gauge.accounts.Count}个账号。按Enter展开详情，F5刷新。";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
        public void Invoke() => gauge.Dispatcher.BeginInvoke(new Action(() => gauge.InvokeRequested?.Invoke()));
    }
}
