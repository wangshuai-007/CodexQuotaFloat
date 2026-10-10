using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>一行一个账号，三个并排进度区同时保留百分比与重置时间。</summary>
public sealed class AccountRow : Border
{
    public string AccountId { get; }
    public int AccountCount { get; set; }
    public event Action<int>? OrderRequested;
    public event Action<bool>? SortingChanged;
    private readonly StackPanel actions = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Hidden };
    internal void ShowActionsForPreview() => actions.Visibility = Visibility.Visible;
    /// <summary>构造可读、可聚焦的额度详情行。</summary>
    public AccountRow(AccountQuota account, int index, DateTimeOffset now)
    {
        AccountId = account.Id;
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
        var controls = new Grid();
        var plan = new TextBlock { Text = account.Plan, FontSize = 13, Foreground = Resource("Muted"),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        controls.Children.Add(plan);
        BuildActions(index);
        controls.Children.Add(actions);
        Grid.SetColumn(controls, 2);
        title.Children.Add(controls);
        stack.Children.Add(title);
        AddMetrics(stack, account, now);
        Child = stack;
        WireActions(plan, index);
    }

    private static void AddMetrics(StackPanel stack, AccountQuota account, DateTimeOffset now)
    {
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
    }

    private void WireActions(TextBlock plan, int index)
    {
        void State()
        {
            var active = IsMouseOver || IsKeyboardFocusWithin;
            actions.Visibility = active ? Visibility.Visible : Visibility.Hidden;
            plan.Visibility = active ? Visibility.Hidden : Visibility.Visible;
            Background = active ? new SolidColorBrush(Color.FromRgb(224, 238, 232)) : Brushes.Transparent;
        }
        MouseEnter += (_, _) => State();
        MouseLeave += (_, _) => State();
        GotKeyboardFocus += (_, _) => State();
        LostKeyboardFocus += (_, _) => State();
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Alt || e.Key is not (Key.Up or Key.Down)) return;
            OrderRequested?.Invoke(index - 1 + (e.Key == Key.Up ? -1 : 1));
            e.Handled = true;
        };
    }

    private void BuildActions(int index)
    {
        foreach (var (label, destination) in new[] { ("置顶", 0), ("最后", int.MaxValue) })
        {
            var button = new Button { Content = label, FontSize = 12, MinWidth = 38,
                Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(2, 0, 0, 0) };
            button.Click += (_, _) => OrderRequested?.Invoke(destination);
            actions.Children.Add(button);
        }
        var grip = new Thumb { Width = 30, Height = 28, Margin = new Thickness(4, 0, 0, 0),
            Cursor = Cursors.SizeNS, Focusable = true, ToolTip = "按住上下拖动排序；Alt+上下键逐行移动" };
        AutomationProperties.SetName(grip, "上下拖动账号顺序");
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(TextBlock.TextProperty, "↕");
        factory.SetValue(TextBlock.FontSizeProperty, 20d);
        factory.SetValue(TextBlock.BackgroundProperty, Resource("Surface"));
        factory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
        var delta = 0d;
        var target = index - 1;
        var hint = new ToolTip { PlacementTarget = grip, Content = "上下拖动" };
        grip.DragStarted += (_, _) => { delta = 0; target = index - 1; SortingChanged?.Invoke(true); Opacity = 0.7; hint.IsOpen = true; };
        grip.DragDelta += (_, e) =>
        {
            delta += e.VerticalChange;
            target = Math.Clamp(index - 1 + (int)Math.Round(delta / Math.Max(1, ActualHeight)), 0, Math.Max(0, AccountCount - 1));
            hint.Content = $"松开移至第 {target + 1} 位";
        };
        grip.DragCompleted += (_, e) =>
        {
            hint.IsOpen = false; Opacity = 1; SortingChanged?.Invoke(false);
            grip.ToolTip = "按住上下拖动排序；Alt+上下键逐行移动";
            if (!e.Canceled && target != index - 1) OrderRequested?.Invoke(target);
        };
        actions.Children.Add(grip);
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
