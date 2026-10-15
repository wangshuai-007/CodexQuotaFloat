using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckForegroundReturnAsync(Window client, IntPtr handle, List<string> checks)
    {
        var own = OwnHandles.ToHashSet();
        Check(!ClientAttachment.KeepInteractionLayer(true, handle, handle, own),
            "返回Codex前台优先恢复层级，不被滞后激活状态阻止", checks);
        Check(ClientAttachment.KeepInteractionLayer(true, new IntPtr(123), handle, own),
            "详情交互期间仍保留已有层级保护", checks);
        Check(foregroundWatcher is not null, "跟随启用时成功注册前台切换监听", checks);
        var other = new Window { Title = "前台切换回归", Width = 100, Height = 80 };
        try
        {
            other.Show();
            coverageReader = _ => false;
            clientTimer.Stop();
            for (var index = 0; index < 3; index++)
            {
                other.Activate();
                pinned = true; Expand(true);
                client.Activate();
                Check(FollowTestSetWindowPos(new WindowInteropHelper(this).Handle, handle,
                    0, 0, 0, 0, 0x0013), "构造返回客户端后浮窗被压到后面的层级", checks);
                var foreground = LayerForegroundWindow();
                FollowTestNotifyWinEvent(3, handle, 0, 0);
                await Task.Delay(150);
                Check(!clientTimer.IsEnabled && IsVisible && !hiddenByClient && AppearsAbove(this, client),
                    $"前台事件独立于500ms轮询恢复浮窗，第{index + 1}次", checks);
                Check(!IsNativeTopmost(this) && LayerForegroundWindow() == foreground,
                    "返回客户端不抢焦点、不使用全局置顶", checks);
            }
        }
        finally
        {
            coverageReader = null;
            other.Close(); Collapse(); client.Activate();
            clientTimer.Start();
        }
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FollowTestSetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "NotifyWinEvent")]
    private static extern void FollowTestNotifyWinEvent(uint eventType, IntPtr window, int objectId, int childId);
}
