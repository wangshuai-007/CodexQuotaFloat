using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

internal static class KeeperPreview
{
    internal static void Run(string output, List<string> report)
    {
        var options = new AppSettings { UseDemo = false, UseKeeper = true,
            KeeperUrl = "http://192.168.1.100:8123/", KeeperPassword = "fixture-only", AllowKeeperLanHttp = true };
        var window = new SettingsWindow(options, "fixture-cpa-key");
        if (window.KeeperSection.Visibility != Visibility.Visible || window.SnapshotSection.Visibility != Visibility.Collapsed ||
            window.KeeperUrlOption.Text != options.KeeperUrl || window.KeeperPasswordOption.Password != "fixture-only")
            throw new InvalidOperationException("Keeper设置显示错误");
        report.Add("通过：Keeper独立地址/密码/HTTP确认显示，旧快照字段隐藏");
        var serialized = JsonSerializer.Serialize(options);
        if (serialized.Contains("fixture-only") || serialized.Contains("KeeperPassword"))
            throw new InvalidOperationException("Keeper密码写入了设置JSON");
        var restored = JsonSerializer.Deserialize<AppSettings>(serialized)!;
        if (!restored.UseKeeper || restored.KeeperUrl != options.KeeperUrl || !restored.AllowKeeperLanHttp || restored.KeeperPassword != "")
            throw new InvalidOperationException("Keeper设置持久化边界错误");
        report.Add("通过：Keeper地址和选项可持久化，密码明确不落盘");
        var scroll = (ScrollViewer)window.Content;
        scroll.Background = (Brush)Application.Current.FindResource("Surface");
        Render(scroll, output, "preview-keeper-settings.png");
        scroll.ScrollToEnd(); scroll.UpdateLayout();
        if (scroll.ScrollableHeight > 0 && Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) > 1)
            throw new InvalidOperationException("Keeper设置无法滚动到底部操作");
        Render(scroll, output, "preview-keeper-settings-bottom.png");
        report.Add("通过：720高设置视口可滚动至保存操作");
        window.KeeperOption.IsChecked = false;
        if (window.KeeperSection.Visibility != Visibility.Collapsed || window.SnapshotSection.Visibility != Visibility.Visible)
            throw new InvalidOperationException("Keeper/原快照切换错误");
        window.SourceOption.SelectedIndex = 2; window.KeeperOption.IsChecked = true;
        if (window.CpaSection.Visibility != Visibility.Collapsed || window.KeeperSection.Visibility != Visibility.Visible)
            throw new InvalidOperationException("本地额度与Keeper流量不能独立使用");
        report.Add("通过：Keeper可与CPA/本地额度独立搭配，切换恢复原快照");
        window.Close();
    }

    private static void Render(FrameworkElement element, string output, string name)
    {
        element.Measure(new Size(570, 720)); element.Arrange(new Rect(0, 0, 570, 720)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(570, 720, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name)); encoder.Save(file);
    }
}
