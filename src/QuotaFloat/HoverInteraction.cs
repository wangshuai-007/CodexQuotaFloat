using System.Windows;
using System.Windows.Input;

namespace QuotaFloat;

public partial class MainWindow
{
    private bool trafficPeek;
    private bool waterPeek;

    private void ShowPeekAt(double y)
    {
        if (hiddenByClient || dragging || settingsOpen || details.IsVisible || menu.IsOpen) return;
        trafficPeek = y >= 208 && y <= 268;
        waterPeek = !trafficPeek && (y < QuotaGauge.AccountsTop || y >= QuotaGauge.AccountsTop + QuotaGauge.AccountsHeight || DisplayAccounts.Count == 0);
        if (trafficPeek || waterPeek) Gauge.ClearHighlight();
        else Gauge.SelectAt(y);
        RevealPeek();
    }

    private void RevealPeek()
    {
        hoverTimer.Stop();
        var selected = Gauge.Viewport.Selected;
        var id = selected >= 0 && selected < DisplayAccounts.Count ? DisplayAccounts[selected].Id : null;
        if (!peek.IsVisible || peek.IsTraffic != trafficPeek || peek.IsWater != waterPeek ||
            (!trafficPeek && !waterPeek && peek.AccountId != id)) UpdatePeek();
        PositionPeek();
        peek.Reveal(settings.ReducedMotion);
        PositionPeek();
    }

    private void UpdatePeek()
    {
        peek.KeepOnTop();
        if (waterPeek)
        {
            peek.UpdateWater(DisplayAccounts, DateTimeOffset.Now,
                settings.UseDemo ? "演示额度" : settings.UseLocalAuth ? "本地Codex" : "CPA", session.Error);
            return;
        }
        if (trafficPeek)
        {
            peek.UpdateTraffic(traffic.Snapshot.ForDisplay(DateTimeOffset.Now), settings.UseDemo);
            return;
        }
        var selected = Gauge.Viewport.Selected;
        var account = selected >= 0 && selected < DisplayAccounts.Count ? DisplayAccounts[selected] : null;
        peek.Update(account, selected, DisplayAccounts.Count, settings.UseDemo, session.Error, session.Loading, settings.UseLocalAuth && !settings.UseDemo);
    }

    private void PositionPeek()
    {
        var area = ScreenPlacement.WorkArea(this);
        peek.Width = Math.Min(304, area.Width - 16);
        peek.UpdateLayout();
        NativePlacement.PlacePopup(this, peek, trafficPeek ? 238 : waterPeek ? 24 : Gauge.SelectedY);
    }

    private void PositionVisiblePanels()
    {
        if (details.IsVisible) PositionDetails();
        if (peek.IsVisible) PositionPeek();
    }

    private void GaugeWheel(object sender, MouseWheelEventArgs e)
    {
        if (details.IsVisible || dragging || settingsOpen) return;
        if (trafficPeek || waterPeek) { e.Handled = true; return; }
        wheelRemainder += e.Delta;
        var steps = -wheelRemainder / 120;
        wheelRemainder %= 120;
        if (steps != 0) ScrollAccounts(steps);
        e.Handled = true;
    }

    private void ScrollAccounts(int steps)
    {
        trafficPeek = false;
        waterPeek = false;
        Gauge.Scroll(steps);
        RevealPeek();
    }

    private void ToggleAll()
    {
        if (details.IsVisible) { Collapse(); return; }
        pinned = true;
        Expand(true);
    }
}
