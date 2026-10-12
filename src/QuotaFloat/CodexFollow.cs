using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace QuotaFloat;

public partial class MainWindow
{
    private readonly DispatcherTimer clientTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool hiddenByClient;
    private bool restorePinnedDetails;
    private Func<bool?> clientVisibilityReader = CodexClientWindows.HasVisibleClient;
    private readonly MenuItem followMenu = new() { Header = "跟随 Codex 显示/隐藏", IsCheckable = true };

    private void WireClientFollow()
    {
        followMenu.Click += (_, _) =>
        {
            settings.FollowCodex = followMenu.IsChecked;
            SavePosition();
            ConfigureClientFollow();
        };
        menu.Items.Insert(3, followMenu);
        menu.Opened += (_, _) => followMenu.IsChecked = settings.FollowCodex;
        clientTimer.Tick += (_, _) => ApplyClientVisibility(clientVisibilityReader());
        Loaded += (_, _) => ConfigureClientFollow();
        Closed += (_, _) => clientTimer.Stop();
    }

    private void ConfigureClientFollow()
    {
        followMenu.IsChecked = settings.FollowCodex;
        if (settings.FollowCodex) clientTimer.Start();
        else clientTimer.Stop();
        ApplyClientVisibility(settings.FollowCodex ? clientVisibilityReader() : true);
    }

    private void ApplyClientVisibility(bool? clientVisible)
    {
        if (settingsOpen || dragging) return;
        if (clientVisible is null)
        {
            tray.Text = "Codex 额度浮窗 · 无法读取客户端窗口状态";
            return; // 读取失败不猜测状态，不反复闪烁或强制隐藏。
        }
        if (settings.FollowCodex && clientVisible == false)
        {
            if (hiddenByClient) return;
            restorePinnedDetails = pinned && details.IsVisible;
            hiddenByClient = true;
            hoverTimer.Stop();
            menu.IsOpen = false;
            peek.Hide();
            details.Hide();
            Gauge.ClearHighlight();
            Hide();
            return;
        }
        if (!hiddenByClient) return;
        hiddenByClient = false;
        RestorePosition();
        var activated = ShowActivated;
        ShowActivated = false;
        Show();
        ShowActivated = activated;
        if (restorePinnedDetails && pinned) Expand();
        restorePinnedDetails = false;
    }

    private void ShowFromTray(bool showDetails = false)
    {
        settings.FollowCodex = false;
        ConfigureClientFollow();
        RestorePosition();
        Show();
        Activate();
        SavePosition();
        if (showDetails) { pinned = true; Expand(true); }
    }
}
