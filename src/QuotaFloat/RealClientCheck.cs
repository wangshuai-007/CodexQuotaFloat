using System.Text;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    /// <summary>验证真实客户端识别及本工具附着，不移动、激活或最小化客户端。</summary>
    public async Task CheckRealClientAsync(string output)
    {
        Directory.CreateDirectory(output);
        var found = CodexClientWindows.FindClient();
        if (found is null || found == IntPtr.Zero) throw new InvalidOperationException("未识别到真实Codex桌面窗口");
        ShowActivated = false;
        Show();
        await Task.Delay(900);
        var checks = new List<string>();
        Check(attachedClient == found, "真实客户端窗口识别并用于附着，不再只按Codex.exe名称匹配", checks);
        Check(!IsNativeTopmost(this) && settings.FollowCodex, "真实附着保持跟随开关并取消全局置顶", checks);
        Check(new WindowInteropHelper(this).Handle != found, "仅调整本工具窗口，不修改真实客户端", checks);
        if (IsVisible)
        {
            var floating = new WindowInteropHelper(this).Handle;
            var cursor = found.Value;
            var above = false;
            while ((cursor = LayerPreviousWindow(cursor, 3)) != IntPtr.Zero)
                if (cursor == floating) { above = true; break; }
            Check(above, "真实桌面上浮窗排在Codex之上，不只检查Visible标志", checks);
        }
        File.WriteAllLines(Path.Combine(output, "real-client-checks.txt"), checks.Append(
            "限制：未移动、最小化或激活真实Codex，未人工验证浏览器全屏。"), new UTF8Encoding(false));
        Close();
    }
}
