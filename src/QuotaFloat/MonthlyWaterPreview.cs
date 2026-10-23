using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>离线验证单月、混合三栏及月过期状态，不读取真实认证。</summary>
internal static class MonthlyWaterPreview
{
    internal static void Run(string output, List<string> report)
    {
        var now = DateTimeOffset.Now;
        var free = new AccountQuota("free", "free@demo.local", "Free", null, null, null, null)
            { AdditionalWindows = [new(2592000, 75, now.AddDays(20))] };
        var paid = new AccountQuota("plus", "plus@demo.local", "Plus", 90, 40, now.AddHours(2), now.AddDays(3));
        var root = new Canvas { Width = 450, Height = 390, Background = (Brush)Application.Current.FindResource("Surface") };
        AddGauge(root, [free], QuotaDisplayMode.Split, 60, "仅月额度", 1, 75);
        AddGauge(root, [paid, free], QuotaDisplayMode.Split, 205, "5h / 7d / m", 3, 75);
        AddGauge(root, [free with { AdditionalWindows = [new(2592000, 75, now.AddDays(-1))] }], QuotaDisplayMode.Split, 350, "月额度到期", 1, 0);
        Save(root, Path.Combine(output, "preview-month-water.png"), 450, 390);
        var details = new DetailWindow();
        details.Update([free], true, false, false, null, false, now, mode: QuotaDisplayMode.Split);
        if (details.WaterLabel.Text != "75" || !details.WaterTitle.Text.Contains("月额度"))
            throw new InvalidOperationException("月账号总体摘要错误");
        details.Update([paid, free], true, false, false, null, false, now, mode: QuotaDisplayMode.Split);
        if (details.WaterLabel.Text != "90% / 40% / 75%") throw new InvalidOperationException("混合三栏摘要错误");
        Save((FrameworkElement)details.Content, Path.Combine(output, "preview-month-detail.png"), 600, 720);
        details.Close();
        var peek = new AccountPeekWindow();
        peek.UpdateWater([paid, free], now, "演示", null);
        Save((FrameworkElement)peek.Content, Path.Combine(output, "preview-month-water-hover.png"), 304, 195);
        peek.Close();
        report.Add("通过：仅月账号单栏m、混合5h/7d/m、过期未知及悬停/摘要独立百分比");
    }

    private static void AddGauge(Canvas root, IReadOnlyList<AccountQuota> accounts, QuotaDisplayMode mode,
        double x, string label, int expected, double monthly)
    {
        var gauge = new QuotaGauge { DisplayMode = mode };
        gauge.Update(accounts, "演示", DateTimeOffset.Now, false, false);
        if (gauge.VisibleWaterColumns.Count != expected || gauge.MonthlyLevel != monthly)
            throw new InvalidOperationException("月水位栏数或数值错误");
        Canvas.SetLeft(gauge, x); Canvas.SetTop(gauge, 70); root.Children.Add(gauge);
        var heading = new TextBlock { Text = label, FontSize = 13 };
        Canvas.SetLeft(heading, x - 20); Canvas.SetTop(heading, 30); root.Children.Add(heading);
    }

    private static void Save(FrameworkElement element, string output, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width * 2, height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(output); encoder.Save(file);
    }
}
