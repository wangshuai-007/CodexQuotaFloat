using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckSystemMenuAsync(List<string> checks)
    {
        var area = ScreenPlacement.WorkArea(this);
        Left = area.Left + 100;
        Top = area.Top + 100;
        var gauge = NativePlacement.Bounds(this);
        var x = (int)gauge.Left + 12;
        var y = (int)gauge.Top + 20;
        var owner = new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Forms.Form { Text = "原生右键菜单测试" };
                var popup = CreatePopupMenu();
                try
                {
                    if (popup == IntPtr.Zero || !AppendMenu(popup, 0, 1, "测试菜单项"))
                        throw new InvalidOperationException("无法创建原生测试菜单");
                    form.Show();
                    form.Activate();
                    owner.SetResult(form.Handle);
                    TrackPopupMenu(popup, 0x180, x, y, 0, form.Handle, IntPtr.Zero);
                }
                finally { if (popup != IntPtr.Zero) DestroyMenu(popup); }
                closed.SetResult();
            }
            catch (Exception error) { owner.TrySetException(error); closed.TrySetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var handle = await owner.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            for (var attempt = 0; !menuHasPriority && attempt < 40; attempt++) await Task.Delay(50);
            var popup = WindowFromPoint(new ScreenPoint(x + 8, y + 8));
            Check(menuHasPriority && ClientAttachment.IsAbove(popup, new WindowInteropHelper(this).Handle) &&
                !IsNativeTopmost(this) && !peek.IsVisible, "原生右键菜单覆盖并接收本来位于浮窗内的位置", checks);
        }
        finally
        {
            PostMessage(handle, 0x001F, IntPtr.Zero, IntPtr.Zero); // WM_CANCELMODE
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        for (var attempt = 0; menuHasPriority && attempt < 20; attempt++) await Task.Delay(50);
        Check(!menuHasPriority && IsNativeTopmost(this), "原生右键菜单取消后恢复浮窗层级", checks);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ScreenPoint(int X, int Y);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(ScreenPoint point);
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, nuint item, string text);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr owner, IntPtr bounds);
    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
