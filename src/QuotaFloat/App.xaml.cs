using System.Windows;
using System.Windows.Threading;

namespace QuotaFloat;

/// <summary>Windows应用入口；支持离线原生UI渲染与正常浮窗模式。</summary>
public partial class App : Application
{
    private Mutex? instanceLock;
    private bool ownsLock;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += HandleFatalError;
        if (e.Args.Contains("--render-preview"))
        {
            try
            {
                var index = Array.IndexOf(e.Args, "--render-preview");
                var output = index + 1 < e.Args.Length ? e.Args[index + 1] : Path.Combine(AppContext.BaseDirectory, "preview");
                await PreviewHarness.RunAsync(Path.GetFullPath(output));
                Shutdown(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "preview-error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--smoke-test"))
        {
            var index = Array.IndexOf(e.Args, "--smoke-test");
            var output = index + 1 < e.Args.Length ? e.Args[index + 1] : AppContext.BaseDirectory;
            try
            {
                var testWindow = new MainWindow(new AppSettings { UseDemo = true, DemoCount = 3 }, "", false);
                MainWindow = testWindow;
                await testWindow.RunSmokeTestAsync(Path.GetFullPath(output));
                Shutdown(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "native-error.txt"), error.ToString());
                Shutdown(1);
            }
            return;
        }
        instanceLock = new Mutex(true, "Local\\CodexQuotaFloat", out ownsLock);
        if (!ownsLock)
        {
            MessageBox.Show("浮窗已在运行。双击任务栏托盘图标可重新显示。", "Codex 额度浮窗");
            Shutdown();
            return;
        }
        var (settings, warning) = SettingsStore.Load();
        if (e.Args.Contains("--demo")) settings.UseDemo = true;
        var key = settings.UseLocalAuth && !settings.UseDemo ? "" : QuotaFloat.Core.ManagementKeyResolver.Read();
        var window = new MainWindow(settings, key);
        MainWindow = window;
        window.Show();
        if (warning is not null) MessageBox.Show(window, warning, "Codex 额度浮窗", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void HandleFatalError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show("应用发生未处理错误，浮窗将关闭。请保留复现步骤后重新启动。\n错误类型：" +
            e.Exception.GetType().Name, "Codex 额度浮窗", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsLock) instanceLock?.ReleaseMutex();
        instanceLock?.Dispose();
        base.OnExit(e);
    }
}
