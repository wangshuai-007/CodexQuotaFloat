using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckAttachedPeekLayerAsync(List<string> checks)
    {
        coverageReader = _ => false;
        var cover = new Window { Title = "提示层级测试", Width = 340, Height = 280,
            WindowStyle = WindowStyle.None, ShowInTaskbar = true };
        try
        {
            ShowPeekAt(50);
            var bounds = NativePlacement.Bounds(peek);
            cover.Show();
            NativePlacement.Move(cover, new Point(bounds.Left, bounds.Top));
            cover.Activate();
            var foreground = LayerForegroundWindow();
            foreach (var y in new[] { 50d, 12d, 230d })
            {
                ShowPeekAt(y);
                await Task.Delay(650);
                Check(peek.IsVisible && IsNativeTopmost(peek) && !IsNativeTopmost(this) && !IsNativeTopmost(details),
                    "附着时悬停提示独立置顶，不提升主浮窗或列表：" + y, checks);
                var cursor = new WindowInteropHelper(cover).Handle;
                var above = false;
                while ((cursor = LayerPreviousWindow(cursor, 3)) != IntPtr.Zero)
                    if (cursor == new WindowInteropHelper(peek).Handle) { above = true; break; }
                Check(above && LayerForegroundWindow() == foreground,
                    "提示在普通覆盖窗之上且不抢前台焦点：" + y, checks);
            }
            peek.Hide();
            Check(!peek.IsVisible, "关闭悬停提示立即隐藏，无残留置顶窗口", checks);
        }
        finally { cover.Close(); coverageReader = null; }
    }

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr LayerForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindow")]
    private static extern IntPtr LayerPreviousWindow(IntPtr window, uint command);
}
