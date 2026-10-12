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
            clientHandleReader = () => CodexClientWindows.IsAvailable(secondHandle) == true ? secondHandle : handle;
            settings.FollowCodex = true;
            ConfigureClientFollow();
            Check(IsVisible && clientTimer.IsEnabled, "跟随开关启动500ms窗口状态检查，可见客户端保持浮窗", checks);
            Check(!IsNativeTopmost(this) && !IsNativeTopmost(details), "附着时取消全局置顶，但保留置顶偏好", checks);
            await CheckAttachmentLayersAsync(client, handle, checks);
            await CheckRelativeAttachmentAsync(client, handle, checks);
            await CheckAttachedPeekLayerAsync(checks);
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
            Check(IsVisible && settings.FollowCodex && clientTimer.IsEnabled,
                "托盘显示和双击不清除跟随开关", checks);
            var reopened = new SettingsWindow(settings, "fixture-key");
            Check(reopened.FollowCodexOption.IsChecked == true, "托盘显示后重新打开设置仍勾选跟随", checks);
            reopened.Close();
            settings.FollowCodex = true; client.Close(); second.Close(); ConfigureClientFollow();
            clientHandleReader = () => IntPtr.Zero; ConfigureClientFollow();
            Check(IsVisible && settings.FollowCodex && IsNativeTopmost(this), "找不到客户端自动独立显示，保留跟随开关及置顶偏好", checks);
        }
        finally
        {
            settings.FollowCodex = false; ConfigureClientFollow();
            clientHandleReader = CodexClientWindows.FindClient;
            client.Close(); second.Close();
        }
    }

    private async Task CheckAttachmentLayersAsync(Window client, IntPtr handle, List<string> checks)
    {
        var initial = NativePlacement.Bounds(this);
        var origin = NativePlacement.Bounds(client);
        NativePlacement.Move(client, new Point(origin.Left + 60, origin.Top + 35));
        await Task.Delay(650);
        var moved = NativePlacement.Bounds(this);
        Check(Math.Abs(moved.Left - initial.Left - 60) < 2 && Math.Abs(moved.Top - initial.Top - 35) < 2,
            "附着浮窗随客户端移动，使用物理坐标避免混合DPI偏移", checks);
        var cover = new Window { Title = "覆盖客户端测试", WindowStyle = WindowStyle.None,
            Width = 600, Height = 400, Left = client.Left - 30, Top = client.Top - 30,
            Background = System.Windows.Media.Brushes.White, ShowInTaskbar = true };
        try
        {
            cover.Show(); cover.Activate();
            var covering = new WindowInteropHelper(cover).Handle;
            coverageReader = h => ClientAttachment.CoveredBy(h, covering, OwnHandles);
            await Task.Delay(650);
            Check(ClientAttachment.CoveredBy(handle, covering, OwnHandles) && !IsVisible,
                "注入前台句柄并用真实窗口几何验证完整覆盖时隐藏", checks);
            cover.Hide(); client.Activate();
            coverageReader = h => ClientAttachment.CoveredBy(h, handle, OwnHandles);
            await Task.Delay(650);
            Check(IsVisible && !IsNativeTopmost(this), "返回客户端自动恢复附着，不成为全局置顶", checks);
        }
        finally { coverageReader = null; cover.Close(); }
    }
}
