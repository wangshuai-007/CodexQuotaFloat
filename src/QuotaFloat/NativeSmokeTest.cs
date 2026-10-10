using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    /// <summary>原生窗口冒烟检查；测试不保存设置、不访问网络、不操作系统鼠标。</summary>
    internal async Task RunSmokeTestAsync(string output)
    {
        var checks = new List<string>();
        ShowActivated = false;
        pinned = true;
        Show();
        await Task.Delay(400);
        Check(session.Accounts.Count == 3 && session.Error is null, "原生主窗启动并读入3个演示账号", checks);
        ((Border)Gauge.Parent).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
            { RoutedEvent = Mouse.MouseEnterEvent });
        await Task.Delay(150);
        Check(details.IsVisible && details.VisibleAccountCount == 3, "悬停事件展开真实详情窗口", checks);
        Check(details.Owner == this, "详情所属主窗一致", checks);
        Check(IsNativeTopmost(this) && IsNativeTopmost(details), "系统窗口置顶标志打开", checks);
        SetTopmost(false);
        await Task.Delay(60);
        Check(!IsNativeTopmost(this) && !IsNativeTopmost(details), "系统窗口置顶标志关闭", checks);
        SetTopmost(true);
        var area = ScreenPlacement.WorkArea(this);
        var point = ScreenPlacement.Snap(area, Width, Height, area.Right - Width - 10, area.Top + 100);
        Left = point.X; Top = point.Y;
        PositionDetails();
        await Task.Delay(60);
        Check(Math.Abs(Left + Width - area.Right) < 1, "原生浮窗右边停靠", checks);
        Check(details.Left < Left && area.Contains(new Rect(details.Left, details.Top, details.ActualWidth, details.ActualHeight)),
            "原生详情朝屏幕内展开并在工作区内", checks);
        RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check(!details.IsVisible && !pinned, "Escape事件收起详情并解除固定", checks);
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "native-checks.txt"), checks.Append(
            "限制：真实窗口及WPF事件验证，未进行物理鼠标拖动或多显示器混合DPI验收。"), new UTF8Encoding(false));
        Close();
    }

    private static bool IsNativeTopmost(Window window) =>
        (GetWindowLong(new WindowInteropHelper(window).Handle, -20) & 0x00000008) != 0;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    private static void Check(bool condition, string name, List<string> report)
    {
        if (!condition) throw new InvalidOperationException("原生冒烟验证失败：" + name);
        report.Add("通过：" + name);
    }
}
