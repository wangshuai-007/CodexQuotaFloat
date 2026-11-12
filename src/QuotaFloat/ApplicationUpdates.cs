using System.Windows;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class MainWindow
{
    private AboutWindow? about;
    private UpdateRelease? availableUpdate;

    private void OpenAbout()
    {
        if (about is not null) { about.Activate(); return; }
        about = new AboutWindow(availableUpdate);
        about.Closed += (_, _) => about = null;
        about.Show();
    }

    private async Task CheckStartupUpdateAsync()
    {
        if (!persistSettings) return;
        using var updates = new GitHubUpdates();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            availableUpdate = await updates.CheckAsync(AboutWindow.CurrentVersion, timeout.Token);
            if (availableUpdate is not null)
                tray.ShowBalloonTip(8000, "CodexQuotaFloat 有新版", availableUpdate.Tag + " · 右键打开“关于与更新”下载安装",
                    System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or FormatException or
            System.Text.Json.JsonException or OperationCanceledException or InvalidOperationException or KeyNotFoundException)
        {
            // 后台失败不弹模态对话框；在托盘提示并允许关于页手动重试。
            tray.Text = "Codex 额度浮窗 · 更新检查失败，可在关于页重试";
        }
    }
}
