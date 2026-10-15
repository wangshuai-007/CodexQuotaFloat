using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QuotaFloat;

/// <summary>只监听前台切换通知，不注入其他进程、不改变输入或激活窗口。</summary>
internal sealed class ForegroundWatcher : IDisposable
{
    private readonly WinEventCallback callback;
    private IntPtr hook;

    internal ForegroundWatcher(Action changed)
    {
        callback = (_, _, _, _, _, _, _) => changed();
        hook = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
        WinEventCallback callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
