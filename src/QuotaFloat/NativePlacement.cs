using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

/// <summary>所有跨屏弹窗定位统一使用原生屏幕坐标，避免混合缩放的全局坐标换算。</summary>
internal static class NativePlacement
{
    internal static Rect Bounds(Window window)
    {
        if (!GetWindowRect(new WindowInteropHelper(window).EnsureHandle(), out var rect))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取浮窗位置");
        return rect.ToRect();
    }

    internal static Rect WorkArea(Window window)
    {
        var monitor = MonitorFromWindow(new WindowInteropHelper(window).EnsureHandle(), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取当前显示器工作区");
        return info.Work.ToRect();
    }

    internal static double Scale(Window window) =>
        Math.Max(96u, GetDpiForWindow(new WindowInteropHelper(window).EnsureHandle())) / 96d;

    internal static void Move(Window window, Point point)
    {
        // 不改变尺寸、激活状态或置顶顺序；WPF自行处理跨屏WM_DPICHANGED。
        if (!SetWindowPos(new WindowInteropHelper(window).EnsureHandle(), IntPtr.Zero,
                (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0, 0x0015))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法定位浮窗");
    }

    internal static void PlacePopup(Window owner, Window popup, double? rowY = null)
    {
        var area = WorkArea(owner);
        var anchor = Bounds(owner);
        var scale = Scale(owner);
        if (rowY is { } y)
        {
            var position = owner.PointToScreen(new Point(8, 8 + y));
            anchor = new Rect(position.X, position.Y, 32 * scale, scale);
        }
        var width = popup.Width * scale;
        var height = Math.Max(1, popup.ActualHeight > 0 ? popup.ActualHeight : popup.DesiredSize.Height) * scale;
        if (popup.IsVisible)
        {
            var bounds = Bounds(popup);
            width = bounds.Width;
            height = bounds.Height;
        }
        Move(popup, ScreenPlacement.Detail(area, anchor, width, height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rect ToRect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
