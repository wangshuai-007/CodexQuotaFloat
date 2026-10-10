using System.Windows;
using System.Windows.Input;

namespace QuotaFloat;

public partial class MainWindow
{
    private void ShowPeekAt(double y)
    {
        if (dragging || settingsOpen || details.IsVisible || menu.IsOpen) return;
        Gauge.SelectAt(y);
        RevealPeek();
    }

    private void RevealPeek()
    {
        hoverTimer.Stop();
        var selected = Gauge.Viewport.Selected;
        var id = selected >= 0 && selected < session.Accounts.Count ? session.Accounts[selected].Id : null;
        if (!peek.IsVisible || peek.AccountId != id) UpdatePeek();
        PositionPeek();
        peek.Reveal(settings.ReducedMotion);
        PositionPeek();
    }

    private void UpdatePeek()
    {
        var selected = Gauge.Viewport.Selected;
        var account = selected >= 0 && selected < session.Accounts.Count ? session.Accounts[selected] : null;
        peek.Topmost = settings.AlwaysOnTop;
        peek.Update(account, selected, session.Accounts.Count, settings.UseDemo, session.Error, session.Loading);
    }

    private void PositionPeek()
    {
        var area = ScreenPlacement.WorkArea(this);
        peek.Width = Math.Min(304, area.Width - 16);
        peek.UpdateLayout();
        var height = peek.ActualHeight > 0 ? peek.ActualHeight : 196;
        var point = ScreenPlacement.Detail(area, new Rect(Left + 8, Top + 8 + Gauge.SelectedY, 32, 1), peek.Width, height);
        peek.Left = point.X;
        peek.Top = point.Y;
    }

    private void PositionVisiblePanels()
    {
        if (details.IsVisible) PositionDetails();
        if (peek.IsVisible) PositionPeek();
    }

    private void GaugeWheel(object sender, MouseWheelEventArgs e)
    {
        if (details.IsVisible || dragging || settingsOpen) return;
        wheelRemainder += e.Delta;
        var steps = -wheelRemainder / 120;
        wheelRemainder %= 120;
        if (steps != 0) ScrollAccounts(steps);
        e.Handled = true;
    }

    private void ScrollAccounts(int steps)
    {
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
