using System.Windows;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class SettingsWindow
{
    private CancellationTokenSource? hookCheck;
    private bool hookStatusClosed;
    private bool settingsSaving;

    private void InitializeHookStatus()
    {
        HookStatusSection.Visibility = FollowCodexOption.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Loaded += async (_, _) => { if (FollowCodexOption.IsChecked == true) await RefreshHookStatusAsync(); };
        Closing += (_, e) => { if (settingsSaving) e.Cancel = true; };
        Closed += (_, _) => { hookStatusClosed = true; hookCheck?.Cancel(); };
    }

    private async void FollowOptionChanged(object sender, RoutedEventArgs e)
    {
        if (HookStatusSection is null) return;
        var enabled = FollowCodexOption.IsChecked == true;
        HookStatusSection.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        hookCheck?.Cancel();
        if (enabled && IsLoaded) await RefreshHookStatusAsync();
    }

    private async void HookCheckClicked(object sender, RoutedEventArgs e) => await RefreshHookStatusAsync();

    private async Task<CodexHookStatus?> RefreshHookStatusAsync()
    {
        hookCheck?.Cancel();
        using var request = new CancellationTokenSource();
        hookCheck = request;
        HookStatusSection.Visibility = Visibility.Visible;
        HookCheckButton.IsEnabled = false;
        HookStatusLabel.Text = "正在检测 Codex 钩子信任状态…";
        HookStatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "CodexQuotaFloat.exe");
            var status = await Task.Run(() => CodexHookInspector.CheckAsync(executable, request.Token), request.Token);
            if (hookStatusClosed || request.IsCancellationRequested) return null;
            ShowHookStatus(status);
            return status;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { return null; }
        finally
        {
            if (ReferenceEquals(hookCheck, request))
            {
                hookCheck = null;
                if (!hookStatusClosed) HookCheckButton.IsEnabled = true;
            }
        }
    }

    internal void ShowHookStatus(CodexHookStatus status)
    {
        HookStatusSection.Visibility = status.Ready ? Visibility.Collapsed : Visibility.Visible;
        HookStatusLabel.Text = status.Message;
        HookStatusLabel.Foreground = (System.Windows.Media.Brush)FindResource(status.Ready ? "Week" : "Danger");
    }
}
