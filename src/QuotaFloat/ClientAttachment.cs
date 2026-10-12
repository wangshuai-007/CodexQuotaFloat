using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

/// <summary>只调整本工具位置和窗口层级；不用跨进程Owner，避免客户端退出销毁浮窗。</summary>
internal static class ClientAttachment
{
    internal static bool Place(Window floating, IntPtr client, IEnumerable<IntPtr> ownWindows, Point? offset = null)
    {
        if (!GetWindowRect(client, out var bounds)) return false;
        var scale = Math.Max(96u, GetDpiForWindow(client)) / 96d;
        var position = offset is { } relative
            ? new Point(bounds.Left + relative.X * scale, bounds.Top + relative.Y * scale)
            : new Point(bounds.Right - (floating.Width + 12) * scale,
                bounds.Top + Math.Min(80 * scale, Math.Max(0, bounds.Bottom - bounds.Top - floating.Height * scale)));
        NativePlacement.Move(floating, position);
        var own = ownWindows.ToHashSet();
        var preceding = GetWindow(client, 3);
        while (preceding != IntPtr.Zero && own.Contains(preceding)) preceding = GetWindow(preceding, 3);
        if (preceding != IntPtr.Zero && (GetWindowLong(preceding, -20) & 8) != 0) preceding = new IntPtr(-2);
        return SetWindowPos(new WindowInteropHelper(floating).Handle, preceding, 0, 0, 0, 0, 0x0013);
    }

    internal static bool TryReadOffset(Window floating, IntPtr client, out Point offset)
    {
        offset = default;
        if (!GetWindowRect(client, out var bounds) ||
            !GetWindowRect(new WindowInteropHelper(floating).Handle, out var position)) return false;
        var scale = Math.Max(96u, GetDpiForWindow(client)) / 96d;
        offset = new Point((position.Left - bounds.Left) / scale, (position.Top - bounds.Top) / scale);
        return true;
    }

    internal static bool Covered(IntPtr client, IEnumerable<IntPtr> ownWindows)
        => CoveredBy(client, GetForegroundWindow(), ownWindows);

    internal static bool CoveredBy(IntPtr client, IntPtr foreground, IEnumerable<IntPtr> ownWindows)
    {
        if (foreground == IntPtr.Zero || foreground == client || ownWindows.Contains(foreground)) return false;
        if (!GetWindowRect(client, out var target) || !GetWindowRect(foreground, out var front)) return false;
        if (DwmGetWindowAttribute(client, 9, out var visibleTarget, Marshal.SizeOf<Bounds>()) == 0) target = visibleTarget;
        if (DwmGetWindowAttribute(foreground, 9, out var visibleFront, Marshal.SizeOf<Bounds>()) == 0) front = visibleFront;
        return front.Left <= target.Left && front.Top <= target.Top && front.Right >= target.Right && front.Bottom >= target.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Bounds bounds);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Bounds bounds, int size);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
