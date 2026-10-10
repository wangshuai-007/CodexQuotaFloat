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
        ManagementKey = string.IsNullOrWhiteSpace(managementKey) ? ManagementKeyResolver.Read() : managementKey;
        InitializeComponent();
        Icon = (System.Windows.Media.ImageSource)FindResource("AppIcon");
        TopmostOption.IsChecked = current.AlwaysOnTop;
        DockOption.IsChecked = current.DockToEdge;
        MotionOption.IsChecked = current.ReducedMotion;
        DemoCountOption.Text = current.DemoCount.ToString();
        UrlOption.Text = current.ManagementUrl;
        KeyOption.Password = ManagementKey;
        IntervalOption.Text = current.RefreshSeconds.ToString();
        TrafficUrlOption.Text = current.TrafficSnapshotUrl;
        SourceOption.SelectedIndex = current.UseDemo ? 0 : 1;
    }

    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UrlOption is null) return;
        var real = SourceOption.SelectedIndex == 1;
        UrlOption.IsEnabled = real;
        KeyOption.IsEnabled = real;
        TrafficUrlOption.IsEnabled = real;
        DemoCountOption.IsEnabled = !real;
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DemoCountOption.Text, out var count) || count is < 0 or > 100)
        { ValidationError.Text = "演示账号数量须为 0–100 的整数。"; DemoCountOption.Focus(); return; }
        if (!int.TryParse(IntervalOption.Text, out var interval) || !RefreshInterval.IsValid(interval))
        { ValidationError.Text = "刷新间隔须为 5–3600 秒的整数。"; IntervalOption.Focus(); return; }
        var demo = SourceOption.SelectedIndex == 0;
        if (!demo)
        {
            try { CpaQuotaSource.ValidateUrl(UrlOption.Text); }
            catch (ArgumentException error) { ValidationError.Text = error.Message; UrlOption.Focus(); return; }
            if (!string.IsNullOrWhiteSpace(TrafficUrlOption.Text))
            {
                try { CpaTrafficSource.ResolveUrl(UrlOption.Text, TrafficUrlOption.Text); }
                catch (ArgumentException error) { ValidationError.Text = error.Message; TrafficUrlOption.Focus(); return; }
            }
            if (string.IsNullOrWhiteSpace(KeyOption.Password))
            { ValidationError.Text = "请输入 CPA 管理密钥，或取消并设置环境变量后重新启动。"; KeyOption.Focus(); return; }
        }
        var next = new AppSettings
        {
            UseDemo = demo, AlwaysOnTop = TopmostOption.IsChecked == true,
            DockToEdge = DockOption.IsChecked == true, ReducedMotion = MotionOption.IsChecked == true,
            DemoCount = count, RefreshSeconds = interval, ManagementUrl = UrlOption.Text.Trim().TrimEnd('/'),
            TrafficSnapshotUrl = TrafficUrlOption.Text.Trim(), AccountOrder = Result.AccountOrder.ToList(),
            Left = Result.Left, Top = Result.Top
        };
        try { SettingsStore.Save(next); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { ValidationError.Text = "无法保存本地设置，请检查 settings.json 的目录权限。"; return; }
        Result = next;
        ManagementKey = KeyOption.Password;
        DialogResult = true;
    }
}
