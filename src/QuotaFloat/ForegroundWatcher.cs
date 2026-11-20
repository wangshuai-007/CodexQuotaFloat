using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QuotaFloat;

/// <summary>监听前台切换和系统菜单，不注入其他进程、不改变输入或激活窗口。</summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private const uint Foreground = 3, MenuEnd = 5, PopupStart = 6, PopupEnd = 7;
    private readonly HashSet<IntPtr> popups = [];
    private readonly WinEventCallback callback;
    private IntPtr hook;

    internal ForegroundWatcher(Action changed)
    {
        callback = (_, kind, window, _, _, _, _) =>
        {
            if (kind == Foreground || kind == MenuEnd) popups.Clear();
            // WPF 自己的右键菜单用 Opened/Closed 管理，避免其辅助功能通知重复计入。
            else if (kind == PopupStart && window != IntPtr.Zero && !IsOwnWindow(window)) popups.Add(window);
            else if (kind == PopupEnd) popups.Remove(window);
            changed();
        };
        hook = SetWinEventHook(Foreground, PopupEnd, IntPtr.Zero, callback, 0, 0, 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal bool IsMenuActive()
    {
        // 原生菜单循环给出前台线程状态，独立弹出菜单则可以有自己的窗口。
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (GetGUIThreadInfo(0, ref info) && (info.Flags & 0x1C) != 0 && !IsOwnWindow(info.MenuOwner)) return true;
        popups.RemoveWhere(window => !IsWindowVisible(window));
        return popups.Count != 0;
    }

    private static bool IsOwnWindow(IntPtr window)
        => GetWindowThreadProcessId(window, IntPtr.Zero) == GetCurrentThreadId();

    /// <summary>在创建监听器的UI线程解除监听。</summary>
    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        if (!UnhookWinEvent(hook)) throw new Win32Exception(Marshal.GetLastWin32Error());
        hook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }

    private delegate void WinEventCallback(IntPtr hook, uint eventType, IntPtr window,
        int objectId, int childId, uint thread, uint time);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal int Size;
        internal uint Flags;
        internal IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        internal int CaretLeft, CaretTop, CaretRight, CaretBottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
        WinEventCallback callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
