using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>关于及更新对话框；下载期间可取消，只有用户确认才启动安装程序。</summary>
public sealed class AboutWindow : Window
{
    internal static Version CurrentVersion => new(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0");
    private readonly GitHubUpdates updates = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextBlock status = new() { Text = "更新来自本项目GitHub正式Release", TextWrapping = TextWrapping.Wrap };
    private readonly Button check = new() { Content = "检查更新" };
    private readonly Button install = new() { Content = "下载并安装", IsEnabled = false, Margin = new Thickness(10, 0, 0, 0) };
    private readonly ProgressBar progress = new() { Height = 5, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 12, 0, 12) };
    private UpdateRelease? release;

    /// <summary>沿用项目字体、表面和图标；可传入启动检查已发现的版本。</summary>
    public AboutWindow(UpdateRelease? available = null)
    {
        Title = "关于 CodexQuotaFloat";
        Width = 500; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("Surface");
        Icon = (System.Windows.Media.ImageSource)Application.Current.FindResource("AppIcon");
        var body = new StackPanel { Margin = new Thickness(28) };
        body.Children.Add(new TextBlock { Text = "CodexQuotaFloat", FontSize = 26, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = "Codex 额度水位 · v" + CurrentVersion.ToString(3), Margin = new Thickness(0, 6, 0, 18) });
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("GitHub · wangshuai-007/CodexQuotaFloat"));
        link.Click += (_, _) => OpenRepository();
        var label = new TextBlock(); label.Inlines.Add(link); body.Children.Add(label);
        body.Children.Add(new TextBlock { Text = "启动时自动检查正式版，发现更新后通知；不会静默安装。\n安装到当前用户目录，升级保留设置。安装包尚未数字签名。便携版更新将迁移至安装版。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 20, 0, 16) });
        body.Children.Add(status); body.Children.Add(progress);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(check); actions.Children.Add(install); body.Children.Add(actions); Content = body;
        check.Click += async (_, _) => await CheckAsync();
        install.Click += async (_, _) => await InstallAsync();
        Closed += (_, _) => { cancellation.Cancel(); updates.Dispose(); };
        if (available is not null) SetRelease(available);
    }

    private void OpenRepository()
    {
        try { Process.Start(new ProcessStartInfo(GitHubUpdates.Repository) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        { status.Text = "无法打开浏览器：" + GitHubUpdates.Repository; }
    }

    private void SetRelease(UpdateRelease? found)
    {
        release = found; install.IsEnabled = found is not null;
        status.Text = found is null ? "当前已是最新正式版" : "发现新版 " + found.Tag + "，可下载并安装";
    }

    private async Task CheckAsync()
    {
        check.IsEnabled = install.IsEnabled = false; status.Text = "正在检查GitHub Release…";
        try { SetRelease(await updates.CheckAsync(CurrentVersion, cancellation.Token)); }
        catch (Exception e) when (Handled(e)) { status.Text = UpdateCheckFailure.Describe(e); }
        finally { check.IsEnabled = true; }
    }

    private async Task InstallAsync()
    {
        if (release is null || MessageBox.Show(this, "下载并校验新版后将退出浮窗，打开当前用户安装向导。继续？",
            "更新浮窗", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        check.IsEnabled = install.IsEnabled = false;
        status.Text = "正在下载安装包，可关闭此窗口取消…";
        try
        {
            var path = await updates.DownloadAsync(release, new Progress<int>(value => progress.Value = value), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            status.Text = "校验成功，正在打开安装向导…";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception e) when (Handled(e))
        { status.Text = "更新失败或已取消，原版本未替换。请检查网络、磁盘空间，或从GitHub重新下载安装包。"; }
        finally { check.IsEnabled = true; install.IsEnabled = release is not null; }
    }

    private static bool Handled(Exception e) => e is UpdateCheckFailure or System.Net.Http.HttpRequestException or IOException or
        UnauthorizedAccessException or FormatException or System.Text.Json.JsonException or OperationCanceledException or
        InvalidOperationException or System.ComponentModel.Win32Exception or KeyNotFoundException;
}
