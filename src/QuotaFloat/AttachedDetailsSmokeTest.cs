using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckInactiveClientDetailsAsync(Window client, IntPtr handle, List<string> checks)
    {
        Collapse();
        PositionDetails();
        var bounds = NativePlacement.Bounds(this);
        var other = new Window { Title = "非焦点客户端详情回归", Width = 120, Height = 100,
            WindowStyle = WindowStyle.None, ShowInTaskbar = true };
        try
        {
            other.Show();
            NativePlacement.Move(other, new Point(bounds.Left - 100, bounds.Top + 50));
            other.Activate();
            var otherHandle = new WindowInteropHelper(other).Handle;
            Check(LayerForegroundWindow() != handle && !ClientAttachment.CoveredBy(handle, otherHandle, OwnHandles),
                "Codex可见但无焦点时可进入详情回归场景", checks);
            pinned = true;
            Expand(true);
            foregroundReader = () => new WindowInteropHelper(details).Handle;
            Check(details.IsVisible && AppearsAbove(details, other) && !IsNativeTopmost(details),
                "点击非焦点Codex浮窗后列表在普通窗口之上，不变成全局置顶", checks);
            Check(details.IsActive, "点击详情激活列表窗口", checks);
            var foreground = LayerForegroundWindow();
            await Task.Delay(1250);
            Check(details.IsVisible && AppearsAbove(details, other) && LayerForegroundWindow() == foreground,
                "注入详情前台状态时跨两次附着轮询保持层级与焦点", checks);
            other.Activate();
            foregroundReader = () => otherHandle;
            await Task.Delay(1250);
            Check(details.IsVisible && !details.IsActive && !AppearsAbove(details, other),
                "离开详情后恢复客户端附着层级，不盖住当前应用", checks);
        }
        finally
        {
            foregroundReader = ClientAttachment.ForegroundWindow;
            other.Close(); Collapse(); client.Activate();
        }
    }

    private static bool AppearsAbove(Window higher, Window lower)
    {
        var expected = new WindowInteropHelper(higher).Handle;
        var cursor = new WindowInteropHelper(lower).Handle;
        while ((cursor = LayerPreviousWindow(cursor, 3)) != IntPtr.Zero)
            if (cursor == expected) return true;
        return false;
    }
}
