using System.Windows;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>本地设置；真实管理密钥仅保留在会话内存。</summary>
public partial class SettingsWindow : Window
{
    public AppSettings Result { get; private set; }
    public string ManagementKey { get; private set; }

    public SettingsWindow(AppSettings current, string managementKey)
    {
        Result = current;
        ManagementKey = current.UseLocalAuth && !current.UseDemo ? managementKey :
            string.IsNullOrWhiteSpace(managementKey) ? ManagementKeyResolver.Read() : managementKey;
        InitializeComponent();
        Icon = (System.Windows.Media.ImageSource)FindResource("AppIcon");
        TopmostOption.IsChecked = current.AlwaysOnTop;
        DockOption.IsChecked = current.DockToEdge;
        MotionOption.IsChecked = current.ReducedMotion;
        FollowCodexOption.IsChecked = current.FollowCodex;
        QuotaDisplayOption.SelectedIndex = Enum.IsDefined(current.QuotaDisplay) ? (int)current.QuotaDisplay : 0;
        DemoCountOption.Text = current.DemoCount.ToString();
        UrlOption.Text = current.ManagementUrl;
        KeyOption.Password = ManagementKey;
        IntervalOption.Text = current.RefreshSeconds.ToString();
        TrafficUrlOption.Text = current.TrafficSnapshotUrl;
        KeeperUrlOption.Text = current.KeeperUrl;
        KeeperPasswordOption.Password = string.IsNullOrEmpty(current.KeeperPassword) ? KeeperPasswordEnvironment.Read() : current.KeeperPassword;
        KeeperHttpOption.IsChecked = current.AllowKeeperLanHttp;
        KeeperOption.IsChecked = current.UseKeeper;
        LocalAuthOption.Text = current.LocalAuthPath;
        try { DefaultAuthLabel.Text = "自动：已安装的Codex CLI；无CLI时读取 " + LocalCodexAuth.ResolvePath(null); }
        catch (ArgumentException) { DefaultAuthLabel.Text = "默认CODEX_HOME路径无效，请手动选择认证文件。"; }
        SourceOption.SelectedIndex = current.UseDemo ? 0 : current.UseLocalAuth ? 2 : 1;
    }

    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UrlOption is null) return;
        var real = SourceOption.SelectedIndex == 1;
        if (real && string.IsNullOrWhiteSpace(KeyOption.Password)) KeyOption.Password = ManagementKeyResolver.Read();
        var local = SourceOption.SelectedIndex == 2;
        LocalAuthSection.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        DemoSection.Visibility = SourceOption.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        CpaSection.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        TrafficSection.Visibility = SourceOption.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        UrlOption.IsEnabled = real;
        KeyOption.IsEnabled = real;
        TrafficUrlOption.IsEnabled = real;
        DemoCountOption.IsEnabled = SourceOption.SelectedIndex == 0;
        UpdateTrafficOptions();
    }

    private void KeeperChanged(object sender, RoutedEventArgs e) => UpdateTrafficOptions();

    private void UpdateTrafficOptions()
    {
        if (KeeperSection is null || SnapshotSection is null || SourceOption is null) return;
        var keeper = KeeperOption.IsChecked == true;
        KeeperSection.Visibility = keeper ? Visibility.Visible : Visibility.Collapsed;
        SnapshotSection.Visibility = !keeper && SourceOption.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseAuthClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择本地Codex认证文件",
            Filter = "JSON认证文件 (*.json)|*.json", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) == true) LocalAuthOption.Text = dialog.FileName;
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        var count = Result.DemoCount;
        if (SourceOption.SelectedIndex == 0 && (!int.TryParse(DemoCountOption.Text, out count) || count is < 0 or > 100))
        { ValidationError.Text = "演示账号数量须为 0–100 的整数。"; DemoCountOption.Focus(); return; }
        if (!int.TryParse(IntervalOption.Text, out var interval) || !RefreshInterval.IsValid(interval))
        { ValidationError.Text = "刷新间隔须为 5–3600 秒的整数。"; IntervalOption.Focus(); return; }
        var demo = SourceOption.SelectedIndex == 0;
        var local = SourceOption.SelectedIndex == 2;
        var keeper = KeeperOption.IsChecked == true;
        if (!demo && keeper)
        {
            try { KeeperUrl.Validate(KeeperUrlOption.Text, KeeperHttpOption.IsChecked == true); }
            catch (ArgumentException error) { ValidationError.Text = error.Message; KeeperUrlOption.Focus(); return; }
        }
        if (local)
        {
            try { LocalCodexAuth.ResolvePath(LocalAuthOption.Text); }
            catch (ArgumentException) { ValidationError.Text = "请输入本机认证文件的完整路径，或留空使用默认位置。"; LocalAuthOption.Focus(); return; }
        }
        if (!demo && !local)
        {
            if (!ValidateCpa(keeper)) return;
        }
        var next = new AppSettings
        {
            UseDemo = demo, UseLocalAuth = local, LocalAuthPath = LocalAuthOption.Text.Trim(),
            AlwaysOnTop = TopmostOption.IsChecked == true,
            FollowCodex = FollowCodexOption.IsChecked == true,
            FollowOffsetX = Result.FollowOffsetX, FollowOffsetY = Result.FollowOffsetY,
            DockToEdge = DockOption.IsChecked == true, ReducedMotion = MotionOption.IsChecked == true,
            QuotaDisplay = (QuotaDisplayMode)Math.Clamp(QuotaDisplayOption.SelectedIndex, 0, 2),
            DemoCount = count, RefreshSeconds = interval, ManagementUrl = UrlOption.Text.Trim().TrimEnd('/'),
            TrafficSnapshotUrl = TrafficUrlOption.Text.Trim(), AccountOrder = Result.AccountOrder.ToList(),
            UseKeeper = keeper, KeeperUrl = KeeperUrlOption.Text.Trim(), KeeperPassword = KeeperPasswordOption.Password,
            AllowKeeperLanHttp = KeeperHttpOption.IsChecked == true,
            Left = Result.Left, Top = Result.Top
        };
        try { SettingsStore.Save(next); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { ValidationError.Text = "无法保存本地设置，请检查 settings.json 的目录权限。"; return; }
        Result = next;
        ManagementKey = KeyOption.Password;
        DialogResult = true;
    }

    private bool ValidateCpa(bool keeper)
    {
        try { CpaQuotaSource.ValidateUrl(UrlOption.Text); }
        catch (ArgumentException error) { ValidationError.Text = error.Message; UrlOption.Focus(); return false; }
        if (!keeper && !string.IsNullOrWhiteSpace(TrafficUrlOption.Text))
        {
            try { CpaTrafficSource.ResolveUrl(UrlOption.Text, TrafficUrlOption.Text); }
            catch (ArgumentException error) { ValidationError.Text = error.Message; TrafficUrlOption.Focus(); return false; }
        }
        if (!string.IsNullOrWhiteSpace(KeyOption.Password)) return true;
        ValidationError.Text = "请输入 CPA 管理密钥，或取消并设置环境变量后重新启动。";
        KeyOption.Focus(); return false;
    }
}
