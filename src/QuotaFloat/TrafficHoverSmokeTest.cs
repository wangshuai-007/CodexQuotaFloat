using System.Windows.Controls;
using System.Windows.Input;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckTrafficHoverAsync(List<string> checks)
    {
        ShowPeekAt(230);
        await Task.Delay(120);
        Check(peek.IsVisible && peek.IsTraffic && peek.AccountId is null && !details.IsVisible,
            "流量区域悬停仅显示最近流量，不显示账号或打开全部", checks);
        Check(peek.ActualHeight < 260 && OnSameMonitor(peek), "流量悬停小卡紧凑且在同屏工作区内", checks);
        var selected = Gauge.Viewport.Selected;
        GaugeWheel(this, new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Check(peek.IsTraffic && Gauge.Viewport.Selected == selected, "流量区域滚轮不误切账号", checks);
        await traffic.RefreshAsync(true);
        var body = (StackPanel)((Border)peek.Content).Child;
        Check(((TextBlock)body.Children[2]).Text == traffic.Snapshot.TokenLabel + " Token",
            "流量小卡跟随新快照刷新", checks);
        ShowPeekAt(50);
        Check(!peek.IsTraffic && peek.AccountId == "demo-01" && !details.IsVisible,
            "移回账号区域恢复额度卡", checks);
    }
}
