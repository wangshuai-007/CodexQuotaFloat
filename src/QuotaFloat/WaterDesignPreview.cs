using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

internal static class WaterDesignPreview
{
    internal static void Run(string output, List<string> report)
    {
        Check(Contrast("#206A9D", "#FDFDFC") >= 4.5, "5h标签文字对比度至少4.5:1", report);
        Check(Contrast("#116C55", "#FDFDFC") >= 4.5, "7天标签文字对比度至少4.5:1", report);
        var label = new FormattedText("5h", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text"), 8, Brushes.Black, 1);
        Check(label.Width <= 14, "双栏仅窗口标签适合半栏且无百分比", report);
        Render(75, 78, false, 0, "相近额度", output, report);
        Render(0, 100, false, 1, "零 / 满额", output, report);
        Render(null, 75, false, 2, "左侧未知", output, report);
        Render(null, null, false, 3, "全部未知", output, report);
        Render(75, 78, true, 4, "旧数据", output, report);
        Render(75, 78, false, 5, "CPA Fast", output, report);
    }

    private static void Render(double? five, double? week, bool stale, int index, string name,
        string output, List<string> report)
    {
        var now = DateTimeOffset.Now;
        var gauge = new QuotaGauge { DisplayMode = QuotaDisplayMode.Split };
        gauge.Update([new("design", "设计样例", "Codex", five, week, now.AddHours(4), now.AddDays(6))],
            "演示", now, stale, false);
        gauge.Measure(new Size(32, 280)); gauge.Arrange(new Rect(0, 0, 32, 280));
        var native = new RenderTargetBitmap(32, 280, 96, 96, PixelFormats.Pbgra32); native.Render(gauge);
        var pixels = new byte[32 * 280 * 4]; native.CopyPixels(pixels, 128, 0);
        if (index == 5)
        {
            gauge.SetFastIndicator("FAST");
            gauge.UpdateLayout();
            var active = new RenderTargetBitmap(32, 280, 96, 96, PixelFormats.Pbgra32); active.Render(gauge);
            var fast = new byte[pixels.Length]; active.CopyPixels(fast, 128, 0);
            Check(!fast.AsSpan(32 * 128, 12 * 128).SequenceEqual(pixels.AsSpan(32 * 128, 12 * 128)), "Fast 绘制状态徽标", report);
            Check(!fast.AsSpan(192 * 128, 16 * 128).SequenceEqual(pixels.AsSpan(192 * 128, 16 * 128)), "Fast 绘制液体纹理", report);
            Check(fast.AsSpan(48 * 128, 8 * 128).SequenceEqual(pixels.AsSpan(48 * 128, 8 * 128)) &&
                gauge.Level == 75 && gauge.WeeklyLevel == 78, "Fast 不改变水位数字或水面以上区域", report);
        }
        var left = (200 * 32 + 10) * 4; var right = (266 * 32 + 22) * 4;
        if (five is null or 0) Check(pixels[left + 2] > 220, name + "不伪造左侧水位", report);
        if (week > 0 && !stale) Check(pixels[right + 1] > pixels[right] + 5,
            name + "保留右侧全高渐变水位", report);
        if (stale) Check(pixels[left + 2] > 190, "旧水位降低透明度", report);
        var canvas = new Canvas { Width = index == 5 ? 244 : 220, Height = 340,
            Background = (Brush)Application.Current.FindResource("Surface") };
        canvas.Children.Add(new TextBlock { Text = "左5h / 右7天 · " + name, FontSize = 12,
            Margin = new Thickness(16, 15, 0, 0) });
        Canvas.SetLeft(gauge, index == 5 ? 194 : 94); Canvas.SetTop(gauge, 48); canvas.Children.Add(gauge);
        if (index == 5)
        {
            var header = FastGlyph.MenuHeader("关闭 CPA 全局 Fast");
            var menuSample = new Border
            {
                Child = header, Background = Brushes.White, BorderBrush = (Brush)Application.Current.FindResource("Stroke"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8)
            };
            Canvas.SetLeft(menuSample, 12); Canvas.SetTop(menuSample, 58); canvas.Children.Add(menuSample);
        }
        canvas.Measure(new Size(canvas.Width, canvas.Height)); canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
        var bitmap = new RenderTargetBitmap((int)canvas.Width, (int)canvas.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"preview-water-design-{index}.png")); encoder.Save(stream);
        Check(gauge.Width == 32 && gauge.Height == 280, name + "保留原尺寸", report);
    }

    private static double Contrast(string foreground, string background)
    {
        static double Linear(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
        static double Luma(Color value) => 0.2126 * Linear(value.R) + 0.7152 * Linear(value.G) + 0.0722 * Linear(value.B);
        var f = Luma((Color)ColorConverter.ConvertFromString(foreground));
        var b = Luma((Color)ColorConverter.ConvertFromString(background));
        return (Math.Max(f, b) + 0.05) / (Math.Min(f, b) + 0.05);
    }

    private static void Check(bool condition, string name, List<string> report)
    {
        if (!condition) throw new InvalidOperationException("水位设计检查失败：" + name);
        report.Add("通过：" + name);
    }
}
