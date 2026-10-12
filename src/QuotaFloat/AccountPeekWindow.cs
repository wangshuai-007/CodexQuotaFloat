using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>复用小卡展示当前账号额度或最近流量，悬停不展开全部列表。</summary>
public sealed partial class AccountPeekWindow : Window
{
    private readonly StackPanel body = new();
    public string? AccountId { get; private set; }

    public AccountPeekWindow()
    {
        Icon = (ImageSource)Application.Current.FindResource("AppIcon");
        Width = 304;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Title = "Codex 单账号额度";
        var shell = new Border { Margin = new Thickness(8), Padding = new Thickness(14, 12, 14, 10),
            CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
            BorderBrush = Resource("Stroke"), Background = Resource("Surface"), Child = body };
        shell.Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.12, Color = Color.FromRgb(65, 97, 107) };
        Content = shell;
    }

    /// <summary>替换当前账号详情；显示剩余、时间进度、重置时刻与数据来源。</summary>
    public void Update(AccountQuota? account, int index, int total, bool demo, string? error, bool loading, bool local = false)
    {
        IsTraffic = false;
        IsWater = false;
        Title = "Codex 单账号额度";
        body.Children.Clear();
        AccountId = account?.Id;
        body.Children.Add(new TextBlock { Text = account?.Name ?? "暂无账号", FontSize = 14,
            FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = account?.Name });
        body.Children.Add(new TextBlock { Text = $"{account?.Plan ?? "Codex"} · {(demo ? "演示" : local ? "本地Codex" : "CPA")} · {Math.Max(0, index + 1)}/{total}",
            FontSize = 11, Foreground = Resource("Muted"), Margin = new Thickness(0, 3, 0, 9) });
        if (account is not null)
        {
            var now = DateTimeOffset.Now;
            Metric("5h 剩余", account.FiveHourRemaining, "Five", QuotaMath.Countdown(account.FiveHourReset, now));
            Metric("周剩余", account.WeeklyRemaining, "Week", QuotaMath.Countdown(account.WeeklyReset, now));
            Metric("7d 已走过", QuotaMath.WeekElapsed(account.WeeklyReset, now), "Cycle",
                account.WeeklyReset is { } reset ? $"重置 {reset.LocalDateTime:MM/dd HH:mm}" : "周期未知");
        }
        var warning = error ?? account?.Error ?? (account?.IsExpired(DateTimeOffset.Now) == true ? "窗口到期 · 待刷新" : null);
        body.Children.Add(new TextBlock { Text = warning ?? (loading ? "正在读取额度…" : "上下移动 / 滚轮切换 · 点击浮窗查看全部"),
            FontSize = 11, Foreground = Resource(warning is null ? "Muted" : "Danger"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
    }

    private void Metric(string label, double? value, string color, string time)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.RowDefinitions.Add(new RowDefinition());
        row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
        row.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        var amount = new TextBlock { Text = value is { } percent ? $"{percent:0}%" : "—",
            FontFamily = new FontFamily("Bahnschrift"), FontSize = 12, Foreground = Resource(color) };
        Grid.SetColumn(amount, 1); row.Children.Add(amount);
        var reset = new TextBlock { Text = time, FontSize = 11, Foreground = Resource("Muted"),
            HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = time };
        Grid.SetColumn(reset, 2); row.Children.Add(reset);
        var bar = new ProgressBar { Value = value ?? 0, Foreground = Resource(color), Height = 2,
            Margin = new Thickness(0, 4, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetName(bar, $"{label} {value:0}%");
        Grid.SetRow(bar, 1); Grid.SetColumnSpan(bar, 3); row.Children.Add(bar);
        body.Children.Add(row);
    }

    /// <summary>首次出现淡入；在账号间切换时保持显示，不闪烁也不抢焦点。</summary>
    public void Reveal(bool reducedMotion)
    {
        if (IsVisible) return;
        Show();
        if (!reducedMotion && SystemParameters.ClientAreaAnimation)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100)));
    }

    private static Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
}
