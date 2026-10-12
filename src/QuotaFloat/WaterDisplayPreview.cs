using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>离线验证三种水位及设置序列化，不改写用户设置。</summary>
internal static class WaterDisplayPreview
{
    internal static async Task RunAsync(string output, List<string> report)
    {
        var accounts = await new DemoQuotaSource().ReadAsync(default);
        var root = new Canvas { Width = 420, Height = 390, Background = (Brush)Application.Current.FindResource("Surface") };
        var modes = Enum.GetValues<QuotaDisplayMode>();
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var options = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings { QuotaDisplay = mode }))!;
            var settings = new SettingsWindow(options, "fixture-key");
            Assert(settings.QuotaDisplayOption.SelectedIndex == i, $"水位设置本地序列化及回显：{mode}", report);
            settings.Close();
            var details = new DetailWindow();
            details.Update(accounts, true, false, false, null, false, DateTimeOffset.Now, mode: mode);
            Assert(details.WaterLabel.Text == new[] { "93", "69", "93% / 69%" }[i], $"详情水位与浮窗一致：{mode}", report);
            details.Close();
            var gauge = new QuotaGauge { DisplayMode = mode };
            gauge.Update(accounts, "演示", DateTimeOffset.Now, false, false);
            Assert(Math.Abs(gauge.Level - 280d / 3) < 0.001 && gauge.WeeklyLevel == 69, $"独立水位：{mode}", report);
            Canvas.SetLeft(gauge, 55 + i * 140); Canvas.SetTop(gauge, 75); root.Children.Add(gauge);
            var label = new TextBlock { Text = new[] { "5h 剩余", "7天 剩余", "左5h / 右7天" }[i], FontSize = 13 };
            Canvas.SetLeft(label, 20 + i * 140); Canvas.SetTop(label, 30); root.Children.Add(label);
        }
        Assert(JsonSerializer.Deserialize<AppSettings>("{}")!.QuotaDisplay == QuotaDisplayMode.FiveHour,
            "旧设置缺少水位选项默认5h", report);
        root.Measure(new Size(root.Width, root.Height)); root.Arrange(new Rect(0, 0, root.Width, root.Height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(840, 780, 192, 192, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, "preview-water-modes.png")); encoder.Save(file);
    }

    private static void Assert(bool condition, string name, List<string> report)
    {
        if (!condition) throw new InvalidOperationException("水位UI验证失败：" + name);
        report.Add("通过：" + name);
    }
}
