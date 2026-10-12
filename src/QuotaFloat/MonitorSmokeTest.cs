using System.Windows;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckMonitorsAsync(List<string> checks)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        checks.Add($"环境：实际显示器 {screens.Length} 块");
        foreach (var screen in screens)
        {
            foreach (var right in new[] { false, true })
            {
                Collapse();
                var work = screen.WorkingArea;
                NativePlacement.Move(this, new Point(work.Left + 80, work.Top + 80));
                await Task.Delay(180);
                var area = NativePlacement.WorkArea(this);
                var main = NativePlacement.Bounds(this);
                NativePlacement.Move(this, new Point(right ? area.Right - main.Width : area.Left,
                    area.Bottom - main.Height));
                await Task.Delay(180);
                var label = $"{screen.DeviceName} / {(right ? "右" : "左")}下侧 / {NativePlacement.Scale(this) * 100:0}%";
                ShowPeekAt(190);
                await Task.Delay(180);
                Check(peek.IsVisible && !details.IsVisible && OnSameMonitor(peek),
                    label + " 悬停小卡在同屏工作区内", checks);
                ShowPeekAt(230);
                await Task.Delay(100);
                Check(peek.IsVisible && peek.IsTraffic && OnSameMonitor(peek),
                    label + " 流量悬停小卡在同屏工作区内", checks);
                ShowPeekAt(12);
                await Task.Delay(100);
                Check(peek.IsVisible && peek.IsWater && OnSameMonitor(peek),
                    label + " 总体水位悬停小卡在同屏工作区内", checks);
                ClickGauge();
                await Task.Delay(180);
                Check(details.IsVisible && !peek.IsVisible && OnSameMonitor(details),
                    label + " 点击全部列表在同屏工作区内", checks);
                ClickGauge();
                Check(!peek.IsVisible && !details.IsVisible, label + " 再次点击收起", checks);
            }
        }
        await CheckVisibleCrossScreenAsync(screens, checks);
    }

    private async Task CheckVisibleCrossScreenAsync(System.Windows.Forms.Screen[] screens, List<string> checks)
    {
        if (screens.Length < 2) { checks.Add("限制：单屏环境无法执行实际跨屏回归"); return; }
        ShowPeekAt(120);
        foreach (var screen in screens.Reverse())
        {
            NativePlacement.Move(this, new Point(screen.WorkingArea.Left + 80, screen.WorkingArea.Top + 80));
            await Task.Delay(220);
            Check(peek.IsVisible && OnSameMonitor(peek), screen.DeviceName + " 已打开小卡跟随跨屏", checks);
        }
        ClickGauge();
        foreach (var screen in screens)
        {
            NativePlacement.Move(this, new Point(screen.WorkingArea.Left + 80, screen.WorkingArea.Top + 80));
            await Task.Delay(220);
            Check(details.IsVisible && OnSameMonitor(details), screen.DeviceName + " 已打开全部面板跟随跨屏", checks);
        }
        Collapse();
    }

    private bool OnSameMonitor(Window popup)
    {
        var area = NativePlacement.WorkArea(this);
        var bounds = NativePlacement.Bounds(popup);
        // Win32整数像素舍入可在工作区边缘产生不足一像素偏差。
        area.Inflate(1, 1);
        return area.Contains(bounds) && NativePlacement.WorkArea(popup) == NativePlacement.WorkArea(this);
    }
}
