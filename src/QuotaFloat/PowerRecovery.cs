using System.Windows.Interop;
using System.Windows.Threading;

namespace QuotaFloat;

public partial class MainWindow
{
    private const int PowerBroadcast = 0x0218;
    private const int ResumeAutomatic = 0x0012;
    private HwndSource? powerSource;
    private DispatcherTimer? resumeTimer;

    private void WirePowerRecovery()
    {
        SourceInitialized += (_, _) =>
        {
            powerSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            powerSource?.AddHook(PowerMessage);
        };
        Closed += (_, _) =>
        {
            resumeTimer?.Stop();
            if (powerSource is { IsDisposed: false }) powerSource.RemoveHook(PowerMessage);
        };
    }

    private IntPtr PowerMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == PowerBroadcast && wParam.ToInt64() == ResumeAutomatic)
        {
            resumeTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
                ResumeTick, Dispatcher);
            resumeTimer.Stop();
            resumeTimer.Start();
        }
        return IntPtr.Zero;
    }

    private async void ResumeTick(object? sender, EventArgs e)
    {
        resumeTimer?.Stop();
        await RecoverAfterResumeAsync();
    }

    private Task RecoverAfterResumeAsync()
    {
        session.ResetAfterResume();
        traffic.Configure(settings, managementKey);
        peek.Hide();
        if (!settings.FollowCodex && !dragging && !settingsOpen) RestorePosition();
        FollowClient();
        return RefreshAll();
    }
}
