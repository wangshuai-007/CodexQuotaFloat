using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>原生浮窗交互协调：拖动、边缘吸附、悬停桥接、置顶和退出。</summary>
public partial class MainWindow : Window
{
    private AppSettings settings;
    private readonly bool persistSettings;
    private string managementKey;
    private readonly QuotaSession session;
    private readonly TrafficSession traffic;
    private System.Drawing.Icon? trayIcon;
    private IReadOnlyList<AccountQuota> DisplayAccounts = [];
    private readonly DetailWindow details = new();
    private readonly AccountPeekWindow peek = new();
    private int wheelRemainder;
    private readonly DispatcherTimer hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(330) };
    private readonly DispatcherTimer refreshTimer = new();
    private readonly DispatcherTimer trafficTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly System.Windows.Forms.NotifyIcon tray = new();
    private Point? pressStart;
    private bool pinned;
    private bool dragging;
    private bool settingsOpen;
    private readonly ContextMenu menu = new();

    /// <summary>创建浮窗；可注入只读流量传输用于离线验证，不改用户设置。</summary>
    public MainWindow(AppSettings options, string key, bool persist = true, System.Net.Http.HttpMessageHandler? trafficHandler = null)
    {
        persistSettings = persist;
        settings = options;
        managementKey = key;
        session = new QuotaSession(settings, key);
        traffic = new TrafficSession(settings, key, trafficHandler);
        InitializeComponent();
        Icon = (System.Windows.Media.ImageSource)FindResource("AppIcon");
        Topmost = settings.AlwaysOnTop;
        WireDetails();
        Gauge.InvokeRequested += () => { pinned = true; Expand(true); };
        WireMenu();
        WireTimers();
        if (persist) WireTray();
        session.Changed += UpdateViews;
        traffic.Changed += UpdateTraffic;
        SourceInitialized += (_, _) => RestorePosition();
        Loaded += async (_, _) => { details.Owner = this; peek.Owner = this; await RefreshAll(); };
        LocationChanged += (_, _) => PositionVisiblePanels();
        DpiChanged += (_, _) => PositionVisiblePanels();
        PreviewKeyDown += OnKeyDown;
        Closing += (_, _) => SavePosition();
        Closed += (_, _) =>
        {
            refreshTimer.Stop(); trafficTimer.Stop(); clockTimer.Stop(); hoverTimer.Stop();
            tray.ContextMenuStrip?.Dispose(); tray.Dispose(); trayIcon?.Dispose();
            session.Dispose(); traffic.Dispose(); details.Close(); peek.Close();
        };
    }

    private void WireDetails()
    {
        details.RefreshRequested += async () => await RefreshAll(true);
        details.OrderRequested += (id, destination) =>
        {
            settings.AccountOrder = AccountOrdering.Move(session.Accounts, settings.AccountOrder, id, destination).ToList();
            details.ShowOrderDestination(destination, session.Accounts.Count);
            SavePosition();
            UpdateViews();
        };
        details.SettingsRequested += OpenSettings;
        details.CollapseRequested += Collapse;
        details.PinRequested += value => pinned = value;
        details.TopmostRequested += SetTopmost;
        details.MouseEnter += (_, _) => hoverTimer.Stop();
        details.MouseLeave += (_, _) => hoverTimer.Start();
        peek.MouseEnter += (_, _) => hoverTimer.Stop();
        peek.MouseLeave += (_, _) => hoverTimer.Start();
        peek.DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(PositionVisiblePanels));
        details.DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(PositionVisiblePanels));
        peek.SizeChanged += (_, _) => { if (peek.IsVisible) PositionPeek(); };
        details.SizeChanged += (_, _) => { if (details.IsVisible) PositionDetails(); };
    }

    private void WireTimers()
    {
        hoverTimer.Tick += (_, _) =>
        {
            if (pinned || dragging || settingsOpen || menu.IsOpen || Gauge.IsMouseOver || details.IsMouseOver || peek.IsMouseOver) return;
            hoverTimer.Stop();
            details.Hide();
            peek.Hide();
            Gauge.ClearHighlight();
        };
        refreshTimer.Interval = TimeSpan.FromSeconds(settings.RefreshSeconds);
        refreshTimer.Tick += async (_, _) => { if (!session.AutoRefreshPaused) await session.RefreshAsync(); };
        trafficTimer.Tick += async (_, _) => { UpdateTraffic(); await traffic.RefreshAsync(); };
        clockTimer.Tick += (_, _) => UpdateViews();
        refreshTimer.Start();
        trafficTimer.Start();
        clockTimer.Start();
    }

    private void WireMenu()
    {
        var top = new MenuItem { Header = "始终置顶", IsCheckable = true, IsChecked = settings.AlwaysOnTop };
        top.Click += (_, _) => SetTopmost(top.IsChecked);
        var dock = new MenuItem { Header = "边缘自动停靠", IsCheckable = true, IsChecked = settings.DockToEdge };
        dock.Click += (_, _) => { settings.DockToEdge = dock.IsChecked; SavePosition(); };
        var detail = new MenuItem { Header = "查看全部账号" };
        detail.Click += (_, _) => { pinned = true; Expand(true); };
        var refresh = new MenuItem { Header = "刷新额度    F5" };
        refresh.Click += async (_, _) => await RefreshAll(true);
        var configure = new MenuItem { Header = "设置    Ctrl+," };
        configure.Click += (_, _) => OpenSettings();
        var quit = new MenuItem { Header = "退出浮窗" };
        quit.Click += (_, _) => Close();
        foreach (var item in new object[] { detail, top, dock, refresh, configure, new Separator(), quit }) menu.Items.Add(item);
        menu.Opened += (_, _) => { top.IsChecked = settings.AlwaysOnTop; dock.IsChecked = settings.DockToEdge; };
        menu.Closed += (_, _) => hoverTimer.Start();
        Gauge.ContextMenu = menu;
    }

    private void WireTray()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/App.ico"))!.Stream;
        using var loadedIcon = new System.Drawing.Icon(stream);
        trayIcon = (System.Drawing.Icon)loadedIcon.Clone();
        tray.Icon = trayIcon;
        tray.Text = "Codex 额度浮窗";
        tray.Visible = true;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { RestorePosition(); Show(); Activate(); pinned = true; Expand(true); });
        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("显示浮窗", null, (_, _) => Dispatcher.Invoke(() => { RestorePosition(); Show(); Activate(); }));
        trayMenu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        trayMenu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = trayMenu;
    }

    private void UpdateViews()
    {
        DisplayAccounts = AccountOrdering.Apply(session.Accounts, settings.AccountOrder);
        var label = session.Loading ? "读取中" : session.Error is not null ? "连接失败" : settings.UseDemo ? "演示" : settings.UseLocalAuth ? "本地" : "CPA";
        Gauge.DisplayMode = settings.QuotaDisplay;
        Gauge.Update(DisplayAccounts, label, DateTimeOffset.Now, session.Error is not null, !settings.ReducedMotion);
        details.Update(DisplayAccounts, settings.UseDemo, settings.AlwaysOnTop, pinned,
            session.Error, session.Loading, session.UpdatedAt, settings.UseLocalAuth && !settings.UseDemo, settings.QuotaDisplay);
        tray.Text = $"Codex 额度浮窗 · {label}";
        if (peek.IsVisible) UpdatePeek();
        PositionVisiblePanels();
        UpdateTraffic();
    }

    private Task RefreshAll(bool manual = false)
    {
        if (manual) session.PrepareManualRefresh();
        return Task.WhenAll(manual || !session.AutoRefreshPaused ? session.RefreshAsync() : Task.CompletedTask,
            traffic.RefreshAsync(manual));
    }

    private void UpdateTraffic()
    {
        var snapshot = traffic.Snapshot.ForDisplay(DateTimeOffset.Now);
        Gauge.SetTraffic(snapshot);
        details.SetTraffic(snapshot, settings.UseDemo);
        if (peek.IsVisible && trafficPeek) peek.UpdateTraffic(snapshot, settings.UseDemo);
    }

    private void RestorePosition()
    {
        var point = ScreenPlacement.Restore(this, settings.Left, settings.Top);
        Left = point.X;
        Top = point.Y;
    }

    private void PositionDetails()
    {
        var area = ScreenPlacement.WorkArea(this);
        details.FitToWorkArea(area);
        details.UpdateLayout();
        NativePlacement.PlacePopup(this, details);
    }

    private void Expand(bool focus = false)
    {
        if (dragging || settingsOpen) return;
        hoverTimer.Stop();
        peek.Hide();
        UpdateViews();
        PositionDetails();
        if (!details.IsVisible) details.Reveal(settings.ReducedMotion);
        PositionDetails();
        if (focus) { details.Activate(); details.Focus(); }
    }

    private void Collapse()
    {
        pinned = false;
        hoverTimer.Stop();
        details.Hide();
        peek.Hide();
        Gauge.ClearHighlight();
        Gauge.Focus();
    }

    private void SetTopmost(bool value)
    {
        settings.AlwaysOnTop = value;
        Topmost = value;
        details.Topmost = value;
        peek.Topmost = value;
        SavePosition();
        UpdateViews();
    }

    private void OpenSettings()
    {
        if (settingsOpen) return;
        settingsOpen = true;
        hoverTimer.Stop();
        details.Hide();
        peek.Hide();
        var dialog = new SettingsWindow(settings, managementKey)
            { Owner = this, Topmost = Topmost, MaxHeight = ScreenPlacement.WorkArea(this).Height };
        try
        {
            if (dialog.ShowDialog() != true) return;
            settings = dialog.Result;
            managementKey = dialog.ManagementKey;
            Topmost = settings.AlwaysOnTop;
            refreshTimer.Interval = TimeSpan.FromSeconds(settings.RefreshSeconds);
            session.Configure(settings, managementKey);
            traffic.Configure(settings, managementKey);
            UpdateViews();
            _ = RefreshAll();
        }
        finally { settingsOpen = false; if (pinned) Expand(); }
    }

    private void SavePosition()
    {
        if (!persistSettings) return;
        settings.Left = Left;
        settings.Top = Top;
        try { SettingsStore.Save(settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, "无法保存浮窗位置，请检查本地设置目录权限。", "Codex 额度浮窗", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void GaugeEntered(object sender, MouseEventArgs e) => ShowPeekAt(e.GetPosition(Gauge).Y);
    private void GaugeLeft(object sender, MouseEventArgs e) => hoverTimer.Start();
    private void GaugePressed(object sender, MouseButtonEventArgs e) => pressStart = e.GetPosition(this);

    private void GaugeMoved(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) { ShowPeekAt(e.GetPosition(Gauge).Y); return; }
        if (pressStart is not { } start || dragging) return;
        var movement = e.GetPosition(this) - start;
        if (movement.Length < 5) return;
        dragging = true;
        pressStart = null;
        details.Hide();
        peek.Hide();
        try { DragMove(); }
        finally
        {
            dragging = false;
            var area = ScreenPlacement.WorkArea(this);
            var point = settings.DockToEdge ? ScreenPlacement.Snap(area, Width, Height, Left, Top) :
                ScreenPlacement.Clamp(area, Width, Height, Left, Top);
            Left = point.X; Top = point.Y;
            SavePosition();
            if (pinned) Expand();
            else if (Gauge.IsMouseOver) ShowPeekAt(Mouse.GetPosition(Gauge).Y);
        }
    }

    private void GaugeReleased(object sender, MouseButtonEventArgs e)
    {
        if (pressStart is null) return;
        pressStart = null;
        ToggleAll();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space) { pinned = true; Expand(true); e.Handled = true; }
        if ((e.Key is Key.Up or Key.Down) && !details.IsVisible)
        { ScrollAccounts(e.Key == Key.Down ? 1 : -1); e.Handled = true; }
        if (e.Key == Key.Escape) { Collapse(); e.Handled = true; }
        if (e.Key == Key.F5) { _ = RefreshAll(true); e.Handled = true; }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        { OpenSettings(); e.Handled = true; }
    }
}
