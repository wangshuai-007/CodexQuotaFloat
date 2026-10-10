using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace QuotaFloat;

/// <summary>原生浮窗交互协调：拖动、边缘吸附、悬停桥接、置顶和退出。</summary>
public partial class MainWindow : Window
{
    private AppSettings settings;
    private readonly bool persistSettings;
    private string managementKey;
    private readonly QuotaSession session;
    private readonly DetailWindow details = new();
    private readonly DispatcherTimer hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(330) };
    private readonly DispatcherTimer refreshTimer = new();
    private readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly System.Windows.Forms.NotifyIcon tray = new();
    private Point? pressStart;
    private bool pinned;
    private bool dragging;
    private bool settingsOpen;
    private readonly ContextMenu menu = new();

    public MainWindow(AppSettings options, string key, bool persist = true)
    {
        persistSettings = persist;
        settings = options;
        managementKey = key;
        session = new QuotaSession(settings, key);
        InitializeComponent();
        Topmost = settings.AlwaysOnTop;
        WireDetails();
        Gauge.InvokeRequested += () => { pinned = true; Expand(true); };
        WireMenu();
        WireTimers();
        if (persist) WireTray();
        session.Changed += UpdateViews;
        SourceInitialized += (_, _) => RestorePosition();
        Loaded += async (_, _) => { details.Owner = this; await session.RefreshAsync(); };
        LocationChanged += (_, _) => { if (details.IsVisible) PositionDetails(); };
        DpiChanged += (_, _) => { if (details.IsVisible) PositionDetails(); };
        PreviewKeyDown += OnKeyDown;
        Closing += (_, _) => SavePosition();
        Closed += (_, _) =>
        {
            refreshTimer.Stop(); clockTimer.Stop(); hoverTimer.Stop();
            tray.ContextMenuStrip?.Dispose(); tray.Dispose(); session.Dispose(); details.Close();
        };
    }

    private void WireDetails()
    {
        details.RefreshRequested += async () => await session.RefreshAsync();
        details.SettingsRequested += OpenSettings;
        details.CollapseRequested += Collapse;
        details.PinRequested += value => pinned = value;
        details.TopmostRequested += SetTopmost;
        details.MouseEnter += (_, _) => hoverTimer.Stop();
        details.MouseLeave += (_, _) => hoverTimer.Start();
    }

    private void WireTimers()
    {
        hoverTimer.Tick += (_, _) =>
        {
            if (pinned || dragging || settingsOpen || menu.IsOpen || Gauge.IsMouseOver || details.IsMouseOver) return;
            hoverTimer.Stop();
            details.Hide();
        };
        refreshTimer.Interval = TimeSpan.FromSeconds(settings.RefreshSeconds);
        refreshTimer.Tick += async (_, _) => { if (!session.AutoRefreshPaused) await session.RefreshAsync(); };
        clockTimer.Tick += (_, _) => UpdateViews();
        refreshTimer.Start();
        clockTimer.Start();
    }

    private void WireMenu()
    {
        var top = new MenuItem { Header = "始终置顶", IsCheckable = true, IsChecked = settings.AlwaysOnTop };
        top.Click += (_, _) => SetTopmost(top.IsChecked);
        var dock = new MenuItem { Header = "边缘自动停靠", IsCheckable = true, IsChecked = settings.DockToEdge };
        dock.Click += (_, _) => { settings.DockToEdge = dock.IsChecked; SavePosition(); };
        var detail = new MenuItem { Header = "展开 / 固定详情" };
        detail.Click += (_, _) => { pinned = true; Expand(true); };
        var refresh = new MenuItem { Header = "刷新额度    F5" };
        refresh.Click += async (_, _) => await session.RefreshAsync();
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
        tray.Icon = System.Drawing.SystemIcons.Information;
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
        var label = session.Loading ? "读取中" : session.Error is not null ? "连接失败" : settings.UseDemo ? "演示" : "CPA";
        Gauge.Update(session.Accounts, label, DateTimeOffset.Now, session.Error is not null, !settings.ReducedMotion);
        details.Update(session.Accounts, settings.UseDemo, settings.AlwaysOnTop, pinned,
            session.Error, session.Loading, session.UpdatedAt);
        tray.Text = $"Codex 额度浮窗 · {label}";
        if (details.IsVisible) PositionDetails();
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
        var height = details.ActualHeight > 0 ? details.ActualHeight : 550;
        var point = ScreenPlacement.Detail(area, new Rect(Left, Top, Width, Height), details.Width, height);
        details.Left = point.X;
        details.Top = point.Y;
    }

    private void Expand(bool focus = false)
    {
        if (dragging || settingsOpen) return;
        hoverTimer.Stop();
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
        Gauge.Focus();
    }

    private void SetTopmost(bool value)
    {
        settings.AlwaysOnTop = value;
        Topmost = value;
        details.Topmost = value;
        SavePosition();
        UpdateViews();
    }

    private void OpenSettings()
    {
        if (settingsOpen) return;
        settingsOpen = true;
        hoverTimer.Stop();
        details.Hide();
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
            _ = session.RefreshAsync();
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

    private void GaugeEntered(object sender, MouseEventArgs e) => Expand();
    private void GaugeLeft(object sender, MouseEventArgs e) => hoverTimer.Start();
    private void GaugePressed(object sender, MouseButtonEventArgs e) => pressStart = e.GetPosition(this);

    private void GaugeMoved(object sender, MouseEventArgs e)
    {
        if (pressStart is not { } start || e.LeftButton != MouseButtonState.Pressed || dragging) return;
        var movement = e.GetPosition(this) - start;
        if (movement.Length < 5) return;
        dragging = true;
        pressStart = null;
        details.Hide();
        try { DragMove(); }
        finally
        {
            dragging = false;
            var area = ScreenPlacement.WorkArea(this);
            var point = settings.DockToEdge ? ScreenPlacement.Snap(area, Width, Height, Left, Top) :
                ScreenPlacement.Clamp(area, Width, Height, Left, Top);
            Left = point.X; Top = point.Y;
            SavePosition();
            if (pinned || Gauge.IsMouseOver) Expand();
        }
    }

    private void GaugeReleased(object sender, MouseButtonEventArgs e)
    {
        if (pressStart is null) return;
        pressStart = null;
        pinned = !pinned;
        if (pinned) Expand(true); else Collapse();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space) { pinned = true; Expand(true); e.Handled = true; }
        if (e.Key == Key.Escape) { Collapse(); e.Handled = true; }
        if (e.Key == Key.F5) { _ = session.RefreshAsync(); e.Handled = true; }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
        { OpenSettings(); e.Handled = true; }
    }
}
