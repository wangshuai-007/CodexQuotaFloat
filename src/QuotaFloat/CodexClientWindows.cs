using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuotaFloat;

/// <summary>只读枚举Codex桌面主窗口，不把CLI、普通浏览器标签或工具窗当作客户端。</summary>
internal static class CodexClientWindows
{
    internal static bool? HasVisibleClient()
    {
        var processes = new Dictionary<uint, bool>();
        var lookupFailed = false;
        var result = ReadVisible(handle =>
        {
            var name = new StringBuilder(256);
            GetClassName(handle, name, name.Capacity);
            if (!IsClientClass(name.ToString())) return false;
            GetWindowThreadProcessId(handle, out var id);
            if (processes.TryGetValue(id, out var matched)) return matched;
            try
            {
                using var process = Process.GetProcessById((int)id);
                matched = IsClientProcess(process.ProcessName);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            { matched = false; } // 枚举期间退出的进程不作为已识别客户端。
            catch (Win32Exception) { lookupFailed = true; matched = false; }
            processes[id] = matched;
            return matched;
        });
        return result == false && lookupFailed ? null : result;
    }

    internal static bool IsClientClass(string name) => name.Equals("Chrome_WidgetWin_1", StringComparison.Ordinal);
    internal static bool IsClientProcess(string name) => name.Equals("Codex", StringComparison.OrdinalIgnoreCase);

    internal static bool? ReadVisible(Func<IntPtr, bool> isClient)
    {
        var visible = false;
        var failed = false;
        var success = EnumWindows((handle, _) =>
        {
            if (GetWindow(handle, 4) != IntPtr.Zero || GetWindowTextLength(handle) == 0 || !isClient(handle)) return true;
            if (!IsWindowVisible(handle) || IsIconic(handle)) return true;
            if (DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) != 0) failed = true;
            else if (cloaked == 0) visible = true;
            return true;
        }, IntPtr.Zero);
        return visible ? true : !success || failed ? null : false;
    }

    private delegate bool EnumWindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
}
