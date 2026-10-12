using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuotaFloat;

/// <summary>只读枚举Codex桌面主窗口，不把CLI、普通浏览器标签或工具窗当作客户端。</summary>
internal static class CodexClientWindows
{
    internal static IntPtr? FindClient()
    {
        var processes = new Dictionary<uint, bool>();
        var lookupFailed = false;
        var found = IntPtr.Zero;
        var foreground = GetForegroundWindow();
        var success = EnumWindows((handle, _) =>
        {
            if (GetWindow(handle, 4) != IntPtr.Zero || GetWindowTextLength(handle) == 0) return true;
            var name = new StringBuilder(256);
            GetClassName(handle, name, name.Capacity);
            if (!IsClientClass(name.ToString())) return true;
            GetWindowThreadProcessId(handle, out var id);
            var matched = processes.GetValueOrDefault(id);
            if (!processes.ContainsKey(id))
            {
                try
                {
                    using var process = Process.GetProcessById((int)id);
                    var processName = process.ProcessName;
                    matched = IsClientIdentity(processName, processName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase)
                        ? process.MainModule?.FileName : null);
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException)
                { matched = false; } // 枚举期间退出的进程不作为已识别客户端。
                catch (Win32Exception) { lookupFailed = true; matched = false; }
                processes[id] = matched;
            }
            if (matched && (found == IntPtr.Zero || found != foreground &&
                (handle == foreground || IsAvailable(found) != true && IsAvailable(handle) == true))) found = handle;
            return true;
        }, IntPtr.Zero);
        return !success || lookupFailed && found == IntPtr.Zero ? null : found;
    }

    internal static bool? IsAvailable(IntPtr handle)
    {
        if (!IsWindowVisible(handle) || IsIconic(handle)) return false;
        return DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 ? cloaked == 0 : null;
    }

    internal static bool IsClientClass(string name) => name.Equals("Chrome_WidgetWin_1", StringComparison.Ordinal);
    internal static bool IsClientProcess(string name) => name.Equals("Codex", StringComparison.OrdinalIgnoreCase);

    internal static bool IsClientIdentity(string name, string? executable)
    {
        if (IsClientProcess(name)) return true;
        if (!name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) || executable is null) return false;
        var path = executable.Replace('\\', '/');
        return path.EndsWith("/app/ChatGPT.exe", StringComparison.OrdinalIgnoreCase) &&
            path.Split('/').Any(part => part.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase));
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
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
}
