using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Interop;
using System.ComponentModel;

namespace QuotaFloat;

public partial class MainWindow
{
    private readonly DispatcherTimer clientTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool hiddenByClient;
    private bool restorePinnedDetails;
    private Func<IntPtr?> clientHandleReader = CodexClientWindows.FindClient;
    private IntPtr attachedClient;
    private Func<IntPtr, bool>? coverageReader;
    private bool EffectiveTopmost => attachedClient == IntPtr.Zero && settings.AlwaysOnTop;
    private IEnumerable<IntPtr> OwnHandles => new Window[] { this, details, peek }.Select(w => new WindowInteropHelper(w).Handle);
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
        clientTimer.Tick += (_, _) => FollowClient();
        Loaded += (_, _) => ConfigureClientFollow();
        Closed += (_, _) => clientTimer.Stop();
    }

    private void ConfigureClientFollow()
    {
        followMenu.IsChecked = settings.FollowCodex;
        if (settings.FollowCodex) clientTimer.Start();
        else clientTimer.Stop();
        FollowClient();
    }

    private void FollowClient()
    {
        if (settingsOpen || dragging) return;
        var target = settings.FollowCodex ? clientHandleReader() : IntPtr.Zero;
        var next = target ?? IntPtr.Zero;
        if (next != attachedClient)
        {
            attachedClient = next;
            Topmost = EffectiveTopmost;
            details.Topmost = EffectiveTopmost;
            peek.KeepOnTop();
            if (next == IntPtr.Zero) RestorePosition();
        }
        if (next == IntPtr.Zero)
        {
            ApplyClientVisibility(true);
            if (target is null) tray.Text = "Codex 额度浮窗 · 无法附着，已独立显示";
            else if (settings.FollowCodex) tray.Text = "Codex 额度浮窗 · 未找到客户端，独立显示";
            return;
        }
        var available = CodexClientWindows.IsAvailable(next);
        var covered = coverageReader?.Invoke(next) ?? ClientAttachment.Covered(next, OwnHandles);
        ApplyClientVisibility(available is null ? true : available == true && !covered);
        if (IsVisible && available == true)
        {
            try
            {
                if (!ClientAttachment.Place(this, next, OwnHandles, FollowOffset,
                    interacting: IsActive || details.IsActive)) AttachmentFailed();
            }
            catch (Win32Exception) { AttachmentFailed(); }
            peek.KeepOnTop();
        }
    }

    private void AttachmentFailed()
    {
        attachedClient = IntPtr.Zero;
        Topmost = EffectiveTopmost;
        details.Topmost = EffectiveTopmost;
        peek.KeepOnTop();
        RestorePosition();
        ApplyClientVisibility(true);
        tray.Text = "Codex 额度浮窗 · 附着失败，已独立显示";
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
        RestorePosition();
        hiddenByClient = false;
        Show();
        Activate();
        SavePosition();
        if (showDetails) { pinned = true; Expand(true); }
    }
}
