using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>32×280窄水柱，最多10组账号线，底部显示最近60秒流量。</summary>
public sealed partial class QuotaGauge : FrameworkElement
{
    public const double AccountsTop = 48;
    public const double AccountsHeight = 144;
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(QuotaGauge), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public AccountViewport Viewport { get; } = new();
    private IReadOnlyList<AccountQuota> accounts = [];
    private DateTimeOffset now;
    private string source = "演示";
    private bool unknown = true;
    private bool stale;
    private bool highlight;
    public event Action? InvokeRequested;
    private static readonly Brush Ink = BrushOf("#203038");
    private static readonly Brush Muted = BrushOf("#536D76");
    private static readonly Brush Five = BrushOf("#287CBA");
    private static readonly Brush Week = BrushOf("#148568");
    private static readonly Brush Cycle = BrushOf("#B76621");

    public QuotaGauge()
    {
        Width = 32;
        Height = 280;
        Focusable = true;
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    /// <summary>更新水位并保留选中账号；额度刷新不重置滚动位置。</summary>
    public void Update(IReadOnlyList<AccountQuota> values, string label, DateTimeOffset time, bool oldData, bool animate)
    {
        var selectedId = Viewport.Selected >= 0 && Viewport.Selected < accounts.Count ? accounts[Viewport.Selected].Id : null;
        var same = selectedId is null ? -1 : values.ToList().FindIndex(a => a.Id == selectedId);
        Viewport.Reset(values.Count, same >= 0 ? same : null);
        accounts = values;
        now = time;
        source = label;
        stale = oldData;
        var value = QuotaMath.WaterLevel(values, time);
        unknown = value is null;
        var target = value ?? 0;
        if (animate && SystemParameters.ClientAreaAnimation)
            BeginAnimation(LevelProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)));
        else { BeginAnimation(LevelProperty, null); Level = target; }
        InvalidateVisual();
    }

    /// <summary>鼠标上下移动选中当前可见行，不展开全账号面板。</summary>
    public int SelectAt(double y)
    {
        var index = Viewport.HitTest(y, AccountsTop, AccountsHeight);
        if (index >= 0) Viewport.Select(index);
        highlight = index >= 0;
        InvalidateVisual();
        return index;
    }

    /// <summary>滚动到前后账号，超过10个时移动账号行视口。</summary>
    public int Scroll(int steps)
    {
        Viewport.Step(steps);
        highlight = Viewport.Selected >= 0;
        InvalidateVisual();
        return Viewport.Selected;
    }

    /// <summary>移出后去掉选中反馈，但保留游标和视口。</summary>
    public void ClearHighlight() { highlight = false; InvalidateVisual(); }

    public double SelectedY => Viewport.Center(Viewport.Selected, AccountsTop, AccountsHeight);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var capsule = new Rect(1, 1, 30, 278);
        dc.DrawRoundedRectangle(new LinearGradientBrush(ColorOf("#FFFFFF"), ColorOf("#E9F1F0"), 90),
            new Pen(BrushOf("#CEDDDF"), 1), capsule, 15, 15);
        dc.PushClip(new RectangleGeometry(capsule, 15, 15));
        DrawWater(dc);
        for (var i = 0; i < 15; i++)
            dc.DrawLine(new Pen(BrushOf("#9CB5B9"), 0.7), new Point(3, 48 + i * 10), new Point(i % 5 == 0 ? 7 : 5, 48 + i * 10));
        Text(dc, unknown ? "—" : $"{Level:0}%", 16, 12, 11, Ink, "Bahnschrift");
        Text(dc, stale ? "旧" : source == "演示" ? "演" : source == "CPA" ? "C" : source == "本地" ? "L" : "…", 16, 29, 9, Muted);
        DrawMeters(dc);
        Text(dc, $"{accounts.Count}", 16, 200, 10, Ink, "Bahnschrift");
        DrawTraffic(dc);
        dc.DrawRoundedRectangle(Muted, null, new Rect(11, 270, 10, 2), 1, 1);
        dc.Pop();
        if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(Five, 1.5), new Rect(0.5, 0.5, 31, 279), 15, 15);
    }

    private void DrawMeters(DrawingContext dc)
    {
        for (var slot = 0; slot < Viewport.VisibleCount; slot++)
        {
            var index = Viewport.Start + slot;
            var account = accounts[index];
            var y = Viewport.Center(index, AccountsTop, AccountsHeight);
            if (highlight && index == Viewport.Selected)
                dc.DrawRoundedRectangle(BrushOf("#DBECE8"), new Pen(BrushOf("#89A9AE"), 0.6),
                    new Rect(6, y - 6, 25, 12), 3, 3);
            Meter(dc, account.FiveHourRemaining, Five, y - 3.5);
            Meter(dc, account.WeeklyRemaining, Week, y);
            Meter(dc, QuotaMath.WeekElapsed(account.WeeklyReset, now), Cycle, y + 3.5);
        }
        if (Viewport.Start > 0) Text(dc, "↑", 16, 39, 8, Muted);
        if (Viewport.Start + Viewport.VisibleCount < accounts.Count) Text(dc, "↓", 16, 190, 8, Muted);
    }

    private void DrawWater(DrawingContext dc)
    {
        var y = 278 - Math.Clamp(Level, 0, 100) / 100 * 236;
        var water = new StreamGeometry();
        using (var path = water.Open())
        {
            path.BeginFigure(new Point(0, y), true, true);
            path.BezierTo(new Point(10, y - 3), new Point(20, y + 3), new Point(32, y), true, false);
            path.LineTo(new Point(32, 281), true, false);
            path.LineTo(new Point(0, 281), true, false);
        }
        dc.DrawGeometry(new LinearGradientBrush(ColorOf("#C5E9E8"), ColorOf("#69BCC6"), 90)
            { Opacity = stale ? 0.28 : 0.72 }, null, water);
    }

    private static void Meter(DrawingContext dc, double? value, Brush color, double y)
    {
        dc.DrawRoundedRectangle(BrushOf("#BDCFD1"), null, new Rect(9, y, 20, 1.8), 0.9, 0.9);
        if (value is { } percent)
            dc.DrawRoundedRectangle(color, null, new Rect(9, y, 20 * QuotaMath.Clamp(percent) / 100, 1.8), 0.9, 0.9);
    }

    private void Text(DrawingContext dc, string text, double x, double y, double size, Brush color, string font = "Segoe UI Variable Text")
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(font), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(x - formatted.Width / 2, y));
    }

    private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Brush BrushOf(string hex) => new SolidColorBrush(ColorOf(hex));
    protected override AutomationPeer OnCreateAutomationPeer() => new GaugePeer(this);

    private sealed class GaugePeer(QuotaGauge gauge) : FrameworkElementAutomationPeer(gauge), IInvokeProvider
    {
        protected override string GetNameCore() => $"Codex额度浮窗，{gauge.source}，{gauge.accounts.Count}个账号。上下键切换账号，Enter查看全部。";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
        public void Invoke() => gauge.Dispatcher.BeginInvoke(new Action(() => gauge.InvokeRequested?.Invoke()));
    }
}
