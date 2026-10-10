using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>一行一个账号，三个并排进度区同时保留百分比与重置时间。</summary>
public sealed class AccountRow : Border
{
    /// <summary>构造可读、可聚焦的额度详情行。</summary>
    public AccountRow(AccountQuota account, int index, DateTimeOffset now)
    {
        Padding = new Thickness(16, 12, 16, 14);
        BorderBrush = (Brush)Application.Current.FindResource("Stroke");
        BorderThickness = new Thickness(0, 0, 0, 1);
        Focusable = true;
        AutomationProperties.SetName(this, $"{account.Name}，5小时剩余{account.FiveHourRemaining:0}%，周剩余{account.WeeklyRemaining:0}%，{account.Error}");
        var stack = new StackPanel();
        var title = new Grid();
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        title.ColumnDefinitions.Add(new ColumnDefinition());
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.Children.Add(new TextBlock { Text = $"{index:00}", FontSize = 13, Foreground = Resource("Muted"), VerticalAlignment = VerticalAlignment.Center });
        var name = new TextBlock { Text = account.Name, FontSize = 16, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = account.Name, Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(name, 1);
        title.Children.Add(name);
        var plan = new TextBlock { Text = account.Plan, FontSize = 13, Foreground = Resource("Muted"),
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(plan, 2);
        title.Children.Add(plan);
        stack.Children.Add(title);
        var metrics = new Grid { Margin = new Thickness(32, 10, 0, 0) };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        AddMetric(metrics, 0, "5h 剩余", account.FiveHourRemaining, "Five", QuotaMath.Countdown(account.FiveHourReset, now));
        AddMetric(metrics, 1, "周剩余", account.WeeklyRemaining, "Week", QuotaMath.Countdown(account.WeeklyReset, now));
        AddMetric(metrics, 2, "7d 已走过", QuotaMath.WeekElapsed(account.WeeklyReset, now), "Cycle",
            account.WeeklyReset is { } reset ? $"重置 {reset.LocalDateTime:MM/dd HH:mm}" : "周期未知");
        stack.Children.Add(metrics);
        if (account.Error is not null || account.IsExpired(now))
            stack.Children.Add(new TextBlock { Text = account.Error ?? "窗口已到期 · 刷新后更新水位",
                Foreground = Resource("Danger"), FontSize = 13, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(32, 8, 0, 0) });
        Child = stack;
        MouseEnter += (_, _) => Background = new SolidColorBrush(Color.FromArgb(100, 224, 238, 232));
        MouseLeave += (_, _) => Background = Brushes.Transparent;
        GotKeyboardFocus += (_, _) => Background = new SolidColorBrush(Color.FromRgb(224, 238, 232));
        LostKeyboardFocus += (_, _) => Background = Brushes.Transparent;
    }

    private static void AddMetric(Grid grid, int column, string label, double? value, string color, string subtitle)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, column == 2 ? 0 : 16, 0) };
        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = Resource("Muted") });
        var amount = new TextBlock { Text = value is { } number ? $"{number:0}%" : "—",
            FontSize = 14, FontFamily = new FontFamily("Bahnschrift"), Foreground = Resource(color) };
        Grid.SetColumn(amount, 1);
        heading.Children.Add(amount);
        stack.Children.Add(heading);
        var bar = new ProgressBar { Value = value ?? 0, Foreground = Resource(color), Margin = new Thickness(0, 5, 0, 7) };
        AutomationProperties.SetName(bar, $"{label}：{(value is { } percent ? $"{percent:0}%" : "未知")}");
        stack.Children.Add(bar);
        stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = Resource("Muted"),
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = subtitle });
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
}
