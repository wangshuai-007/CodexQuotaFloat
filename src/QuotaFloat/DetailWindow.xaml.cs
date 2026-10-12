using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>最多显示10条账号详情的悬浮窗；分页不影响全体账号汇总。</summary>
public partial class DetailWindow : Window
{
    private IReadOnlyList<AccountQuota> accounts = [];
    private int page;
    private bool updating;
    private DateTimeOffset now;
    private bool sorting;
    public event Action<string, int>? OrderRequested;
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? CollapseRequested;
    public event Action<bool>? PinRequested;
    public event Action<bool>? TopmostRequested;
    public int VisibleAccountCount => Rows.Children.OfType<AccountRow>().Count();

    public DetailWindow()
    {
        InitializeComponent();
        Icon = (System.Windows.Media.ImageSource)FindResource("AppIcon");
        PreviewKeyDown += OnKeyDown;
    }

    /// <summary>刷新信息与错误提示，不覆盖分页位置。</summary>
    public void Update(IReadOnlyList<AccountQuota> values, bool demo, bool topmost, bool pinned,
        string? error, bool loading, DateTimeOffset? refreshed, bool local = false)
    {
        accounts = values;
        now = DateTimeOffset.Now;
        page = Math.Clamp(page, 0, Math.Max(0, (values.Count - 1) / 10));
        var level = QuotaMath.WaterLevel(values, now);
        var valid = values.Count(a => a.Available(now) is not null);
        WaterLabel.Text = level is { } percent ? $"{percent:0}" : "—";
        CountLabel.Text = $"{values.Count} 个账号 · {valid} 个计入水位";
        SourceLabel.Text = $"{(demo ? "演示数据 · 未连接 CPA" : local ? "本地Codex认证 · 直接查询" : "CPA 管理额度")}   /   " +
            (refreshed is { } time ? $"更新于 {time.LocalDateTime:HH:mm:ss}" : "尚未更新");
        Notice.Text = error is not null ? error + (values.Count > 0 ? "；下方为上次快照，不代表当前额度。" : "") :
            loading ? "正在读取额度，保留上一份数据…" :
            values.Count == 0 ? local ? "暂无本地账号额度；请检查ChatGPT登录及认证文件。" : "暂无 Codex 账号。演示账号数量可在设置中调整；CPA 模式请检查账号是否启用。" :
            valid != values.Count ? $"{values.Count - valid} 个账号失败、缺少窗口或已到期，不计入总体水位。" :
            demo ? "当前为假数据。百分比参考示例，邮箱已匿名化；重置时间为相对演示时间。" :
            "只读取额度，不重置、不修改账号；7d 显示固定 7 天周期的已走过时间。";
        Notice.Foreground = (System.Windows.Media.Brush)FindResource(error is null ? "Muted" : "Danger");
        RefreshButton.IsEnabled = !loading;
        RefreshButton.Content = loading ? "读取中" : "刷新";
        Topmost = topmost;
        updating = true;
        TopmostToggle.IsChecked = topmost;
        PinDetails.IsChecked = pinned;
        updating = false;
        if (!sorting) RenderRows();
    }

    /// <summary>按当前工作区限制详情高度，短屏允许滚动。</summary>
    public void FitToWorkArea(Rect area)
    {
        Width = Math.Min(600, area.Width - 20);
        RowScroller.MaxHeight = Math.Max(60, area.Height - 430);
        MaxHeight = area.Height;
    }

    /// <summary>窗口出现时执行可中断淡入，尊重平台减弱动效。</summary>
    public void Reveal(bool reducedMotion)
    {
        if (!IsVisible) Show();
        if (!reducedMotion && SystemParameters.ClientAreaAnimation)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        else
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        }
    }

    /// <summary>切换页码供按钮与自动验证共用。</summary>
    public void ChangePage(int delta)
    {
        page = Math.Clamp(page + delta, 0, Math.Max(0, (accounts.Count - 1) / 10));
        RenderRows();
        RowScroller.ScrollToTop();
    }

    /// <summary>排序完成后显示目标所在页，避免账号移动后无法看到结果。</summary>
    public void ShowOrderDestination(int destination, int count)
    {
        page = Math.Clamp(destination, 0, Math.Max(0, count - 1)) / 10;
        RowScroller.ScrollToTop();
    }

    private void RenderRows()
    {
        var focused = Rows.Children.OfType<AccountRow>().FirstOrDefault(row => row.IsKeyboardFocusWithin)?.AccountId;
        Rows.Children.Clear();
        var visible = QuotaMath.Page(accounts, page);
        for (var i = 0; i < visible.Count; i++)
        {
            var row = new AccountRow(visible[i], page * 10 + i + 1, now);
            row.OrderRequested += destination => OrderRequested?.Invoke(row.AccountId, destination);
            row.SortingChanged += active => sorting = active;
            row.AccountCount = accounts.Count;
            Rows.Children.Add(row);
            if (row.AccountId == focused) row.Focus();
        }
        PageLabel.Text = $"{page + 1} / {Math.Max(1, (accounts.Count + 9) / 10)}";
        Previous.IsEnabled = page > 0;
        Next.IsEnabled = (page + 1) * 10 < accounts.Count;
    }

    /// <summary>独立展示最近60秒CPA全局请求统计；未知不显示为零。</summary>
    public void SetTraffic(TrafficSnapshot value, bool demo)
    {
        TrafficLabel.Text = value.Tokens is null ? "流量未接入 · " + (value.Error ?? "等待只读统计快照") :
            $"{(demo ? "演示流量" : "CPA全局")} · 最近60秒 {value.TokenLabel} Token / {value.Requests} 次";
        TrafficLabel.ToolTip = value.UpdatedAt is { } time ? $"截至 {time.LocalDateTime:HH:mm:ss}，仅已完成请求；每5秒一格" : value.Error;
    }

    private void PinChanged(object sender, RoutedEventArgs e) { if (!updating) PinRequested?.Invoke(PinDetails.IsChecked == true); }
    private void TopmostChanged(object sender, RoutedEventArgs e) { if (!updating) TopmostRequested?.Invoke(TopmostToggle.IsChecked == true); }
    private void RefreshClicked(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();
    private void SettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void CollapseClicked(object sender, RoutedEventArgs e) => CollapseRequested?.Invoke();
    private void PreviousClicked(object sender, RoutedEventArgs e) => ChangePage(-1);
    private void NextClicked(object sender, RoutedEventArgs e) => ChangePage(1);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { CollapseRequested?.Invoke(); e.Handled = true; }
        if (e.Key == Key.F5) { RefreshRequested?.Invoke(); e.Handled = true; }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        { SettingsRequested?.Invoke(); e.Handled = true; }
    }
}
