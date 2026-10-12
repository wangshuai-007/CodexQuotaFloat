using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckCodexFollowAsync(List<string> checks)
    {
        var client = new Window { Title = "跟随测试客户端", Width = 180, Height = 100,
            ShowActivated = false, ShowInTaskbar = true };
        var second = new Window { Title = "跟随测试第二窗口", Width = 180, Height = 100,
            ShowActivated = false, ShowInTaskbar = true };
        try
        {
            client.Show();
            var handle = new WindowInteropHelper(client).Handle;
            var secondHandle = new WindowInteropHelper(second).EnsureHandle();
            clientVisibilityReader = () => CodexClientWindows.ReadVisible(h => h == handle || h == secondHandle);
            settings.FollowCodex = true;
            ConfigureClientFollow();
            Check(IsVisible && clientTimer.IsEnabled, "跟随开关启动500ms窗口状态检查，可见客户端保持浮窗", checks);
            pinned = true; Expand();
            client.WindowState = WindowState.Minimized;
            await Task.Delay(650);
            Check(!IsVisible && !details.IsVisible && !peek.IsVisible && hiddenByClient,
                "客户端最小化后浮窗及固定详情同时隐藏", checks);
            client.WindowState = WindowState.Normal;
            await Task.Delay(650);
            Check(IsVisible && details.IsVisible && pinned, "客户端恢复显示浮窗并恢复固定详情", checks);
            Collapse(); ShowPeekAt(50);
            client.Hide(); await Task.Delay(650);
            Check(!IsVisible && !peek.IsVisible, "客户端隐藏同时关闭账号悬停卡", checks);
            second.Show(); await Task.Delay(650);
            Check(IsVisible && !peek.IsVisible, "任一客户端窗口可见恢复浮窗，不恢复过时悬停卡", checks);
            ApplyClientVisibility(null);
            Check(IsVisible, "无法读取客户端状态时保持现状，不猜测隐藏", checks);
            second.Hide(); await Task.Delay(650);
            Check(!IsVisible, "所有客户端窗口隐藏后隐藏浮窗", checks);
            settings.FollowCodex = false; ConfigureClientFollow();
            Check(IsVisible && !clientTimer.IsEnabled, "关闭跟随立即显示并停止窗口轮询", checks);
            settings.FollowCodex = true; ConfigureClientFollow();
            ShowFromTray();
            Check(IsVisible && !settings.FollowCodex && !clientTimer.IsEnabled,
                "托盘显示浮窗关闭跟随，防止恢复后立即消失", checks);
            settings.FollowCodex = true; client.Close(); second.Close(); ConfigureClientFollow();
            Check(!IsVisible, "客户端窗口关闭后隐藏，保留应用进程", checks);
        }
        finally
        {
            settings.FollowCodex = false; ConfigureClientFollow();
            clientVisibilityReader = CodexClientWindows.HasVisibleClient;
            client.Close(); second.Close();
        }
    }
}
