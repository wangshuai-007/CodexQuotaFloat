using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QuotaFloat;

/// <summary>只调整本工具位置和窗口层级；不用跨进程Owner，避免客户端退出销毁浮窗。</summary>
internal static class ClientAttachment
{
    internal static bool Place(Window floating, IntPtr client, IEnumerable<IntPtr> ownWindows, Point? offset = null,
        bool interacting = false, IntPtr? foreground = null)
    {
        if (!GetWindowRect(client, out var bounds)) return false;
        var scale = Math.Max(96u, GetDpiForWindow(client)) / 96d;
        var position = offset is { } relative
            ? new Point(bounds.Left + relative.X * scale, bounds.Top + relative.Y * scale)
            : new Point(bounds.Right - (floating.Width + 12) * scale,
                bounds.Top + Math.Min(80 * scale, Math.Max(0, bounds.Bottom - bounds.Top - floating.Height * scale)));
        NativePlacement.Move(floating, position);
        var own = ownWindows.ToHashSet();
        // 重排Owner会连带降低详情窗口；交互期间仅跟随位置，离开本工具后恢复附着层级。
        if (KeepInteractionLayer(interacting, foreground ?? GetForegroundWindow(), client, own)) return true;
        var preceding = AttachmentAnchor(client, own);
        // HWND_NOTOPMOST对本来就非置顶的窗口不重排；HWND_TOP才会恢复普通窗口组顶端。
        if (preceding != IntPtr.Zero && (GetWindowLong(preceding, -20) & 8) != 0) preceding = IntPtr.Zero;
        var window = new WindowInteropHelper(floating).Handle;
        if (!SetWindowPos(window, preceding, 0, 0, 0, 0, 0x0213)) return false;
        return IsAbove(window, client) || RecoverLayer(window, client, preceding);
    }

    internal static bool KeepInteractionLayer(bool interacting, IntPtr foreground, IntPtr client,
        IReadOnlySet<IntPtr> own)
        => foreground != client && (foreground != IntPtr.Zero && own.Contains(foreground) ||
            foreground == IntPtr.Zero && interacting);

    internal static IntPtr ForegroundWindow() => GetForegroundWindow();

    internal static bool IsAbove(IntPtr window, IntPtr client)
    {
        var seen = new HashSet<IntPtr>();
        var cursor = GetWindow(client, 3);
        while (cursor != IntPtr.Zero && seen.Add(cursor))
        {
            if (cursor == window) return true;
            cursor = GetWindow(cursor, 3);
        }
        return false;
    }

    internal static bool RecoverLayer(IntPtr window, IntPtr client, IntPtr anchor)
    {
        // 后台进程的HWND_TOP可能成功返回却不抬高。仅失败时瞬时抬升并立即撤销置顶，且不激活。
        if (!SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x0213)) return false;
        if (!SetWindowPos(window, new IntPtr(-2), 0, 0, 0, 0, 0x0213)) return false;
        if (anchor != IntPtr.Zero && !SetWindowPos(window, anchor, 0, 0, 0, 0, 0x0213)) return false;
        return IsAbove(window, client);
    }

    internal static IntPtr AttachmentAnchor(IntPtr client, IReadOnlySet<IntPtr> own)
    {
        var seen = new HashSet<IntPtr>();
        var candidate = GetWindow(client, 3);
        while (candidate != IntPtr.Zero && seen.Add(candidate))
        {
            // IME等隐藏Owned窗口不代表应用层级，拿它作锚点会受Owner分组重排影响。
            if (!own.Contains(candidate) && IsWindowVisible(candidate) &&
                GetWindow(candidate, 4) == IntPtr.Zero && !IsCloaked(candidate)) return candidate;
            candidate = GetWindow(candidate, 3);
        }
        return IntPtr.Zero;
    }

    private static bool IsCloaked(IntPtr window)
        => DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

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
        if (DwmGetWindowAttribute(client, 9, out Bounds visibleTarget, Marshal.SizeOf<Bounds>()) == 0) target = visibleTarget;
        if (DwmGetWindowAttribute(foreground, 9, out Bounds visibleFront, Marshal.SizeOf<Bounds>()) == 0) front = visibleFront;
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Bounds bounds, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
