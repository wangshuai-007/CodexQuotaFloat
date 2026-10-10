using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace QuotaFloat;

public partial class MainWindow
{
    /// <summary>验证真实窗口与鼠标/滚轮事件路径，不保存设置、不访问网络。</summary>
    internal async Task RunSmokeTestAsync(string output)
    {
        var checks = new List<string>();
        ShowActivated = false;
        Show();
        await Task.Delay(400);
        Check(session.Accounts.Count == 3 && session.Error is null, "启动并读取3个演示账号", checks);
        Check(Width == 48 && Height == 240 && Gauge.Width == 32, "窄浮窗尺寸48×240，水柱宽32", checks);
        await CheckHoverAsync(checks);
        await CheckScrollAsync(checks);
        await CheckPanelsAsync(checks);
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "native-checks.txt"), checks.Append(
            "限制：真实窗口及事件路径验证，未进行系统物理鼠标操作或多屏混合DPI验收。"), new UTF8Encoding(false));
        Close();
    }

    private async Task CheckHoverAsync(List<string> checks)
    {
        ShowPeekAt(50);
        await Task.Delay(120);
        Check(peek.IsVisible && !details.IsVisible && peek.AccountId == "demo-01", "上段仅显示第一账号小卡，不打开全部", checks);
        Check(peek.Width == 304 && peek.ActualHeight < 230, "单账号卡宽304、高度小于230", checks);
        ShowPeekAt(120);
        Check(peek.AccountId == "demo-02" && !details.IsVisible, "上下移动切换第二账号", checks);
        ShowPeekAt(190);
        Check(peek.AccountId == "demo-03", "下段切换第三账号", checks);
        ShowPeekAt(50);
        GaugeWheel(this, new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -60) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Check(peek.AccountId == "demo-01", "高精度滚轮半格不误切换", checks);
        GaugeWheel(this, new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -60) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Check(peek.AccountId == "demo-02", "累积滚轮事件切换下一账号", checks);
        Check(peek.Owner == this && IsNativeTopmost(this) && IsNativeTopmost(peek), "小卡Owner和原生置顶标志正确", checks);
        SetTopmost(false);
        await Task.Delay(60);
        Check(!IsNativeTopmost(this) && !IsNativeTopmost(peek), "主浮窗与小卡置顶关闭", checks);
        SetTopmost(true);
    }

    private async Task CheckScrollAsync(List<string> checks)
    {
        settings.DemoCount = 12;
        session.Configure(settings, "");
        await session.RefreshAsync();
        ShowPeekAt(50);
        ScrollAccounts(11);
        Check(Gauge.Viewport.Selected == 11 && Gauge.Viewport.Start == 2 &&
            Gauge.Viewport.VisibleCount == 10 && peek.AccountId == "demo-12", "超过10账号可滚到第十二账号", checks);
        ShowPeekAt(50);
        Check(peek.AccountId == "demo-03", "滚动后上段对应当前视口第三账号", checks);
        var selectedId = peek.AccountId;
        await session.RefreshAsync();
        Check(peek.AccountId == selectedId && Gauge.Viewport.Start == 2, "刷新保留已查看账号和滚动位置", checks);
        Check(!details.IsVisible, "移动/滚轮/刷新均不会打开全部列表", checks);
    }

    private async Task CheckPanelsAsync(List<string> checks)
    {
        var area = ScreenPlacement.WorkArea(this);
        Left = area.Right - Width; Top = area.Top + 100;
        PositionPeek();
        await Task.Delay(60);
        Check(peek.Left < Left && area.Contains(new Rect(peek.Left, peek.Top, peek.ActualWidth, peek.ActualHeight)),
            "右侧小卡向屏幕内展开且不越界", checks);
        ClickGauge();
        await Task.Delay(100);
        Check(details.IsVisible && !peek.IsVisible && details.VisibleAccountCount == 10 && pinned,
            "点击才展示全账号列表并固定", checks);
        ShowPeekAt(190);
        Check(!peek.IsVisible && details.IsVisible, "点击后的全部面板不被悬停小卡覆盖", checks);
        Check(IsNativeTopmost(details), "全账号面板置顶", checks);
        ClickGauge();
        Check(!details.IsVisible && !peek.IsVisible && !pinned, "再次点击关闭全部面板", checks);
        ShowPeekAt(120);
        RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check(!details.IsVisible && !peek.IsVisible, "Escape关闭单账号小卡和全部面板", checks);
    }

    private void ClickGauge()
    {
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.MouseDownEvent };
        GaugePressed(this, click);
        GaugeReleased(this, click);
    }

    private static bool IsNativeTopmost(Window window) =>
        (GetWindowLong(new WindowInteropHelper(window).Handle, -20) & 0x00000008) != 0;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    private static void Check(bool condition, string name, List<string> report)
    {
        if (!condition) throw new InvalidOperationException("原生验证失败：" + name);
        report.Add("通过：" + name);
    }
}
