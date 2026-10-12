using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>渲染每秒速率的可用、首帧及过期状态，不访问CPA。</summary>
internal static class TrafficRatePreview
{
    internal static void Run(string output, List<string> report)
    {
        var now = DateTimeOffset.Now;
        var active = new TrafficSnapshot(4310, 18, [0, 320, 170, 650, 120, 0, 930, 440, 170, 780, 220, 510], now)
            { TokensPerSecond = 1500, RequestsPerSecond = 1.2, SampleSeconds = 1 };
        var states = new[] { active, active with { TokensPerSecond = null, RequestsPerSecond = null },
            active.ForDisplay(now.AddSeconds(4)) };
        var canvas = new Canvas { Width = 480, Height = 390, Background = (Brush)Application.Current.FindResource("Surface") };
        for (var i = 0; i < states.Length; i++) AddState(canvas, states[i], i, report);
        canvas.Measure(new Size(canvas.Width, canvas.Height)); canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
        var bitmap = new RenderTargetBitmap(960, 780, 192, 192, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, "preview-token-rate.png")); encoder.Save(file);
    }

    private static void AddState(Canvas canvas, TrafficSnapshot snapshot, int index, List<string> report)
    {
        var title = new TextBlock { Text = new[] { "60秒平均 / 71.8 tk/s", "首帧 / 可显示平均", "数据过期 / 不显示旧速率" }[index], FontSize = 12 };
        Canvas.SetLeft(title, 12 + index * 160); Canvas.SetTop(title, 25); canvas.Children.Add(title);
        var gauge = new QuotaGauge(); gauge.SetTraffic(snapshot);
        Canvas.SetLeft(gauge, 64 + index * 160); Canvas.SetTop(gauge, 65); canvas.Children.Add(gauge);
        var details = new DetailWindow(); details.SetTraffic(snapshot, false);
        if (!details.TrafficLabel.Text.Contains("60秒平均 Token/s") || !details.TrafficLabel.Text.Contains(snapshot.AverageTokenRateLabel) ||
            details.TrafficLabel.Text.Contains("请求/s") || details.TrafficLabel.Text.Contains(" 次"))
            throw new InvalidOperationException("Token/s状态未正确显示");
        details.Close(); report.Add("通过：每秒速率状态显示：" + title.Text);
    }
}
