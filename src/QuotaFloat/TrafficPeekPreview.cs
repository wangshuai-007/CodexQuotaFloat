using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

internal static class TrafficPeekPreview
{
    internal static void Run(string output, List<string> report)
    {
        var now = DateTimeOffset.Now;
        var active = new TrafficSnapshot(4310, 18, [0, 320, 170, 650, 120, 0, 930, 440, 170, 780, 220, 510], now)
            { SourceLabel = "Keeper · CPA全局", TokensPerSecond = 1500 };
        var states = new[] { active, active with { Tokens = 0, Buckets = new long[12] },
            TrafficSnapshot.Unknown("Keeper HTTP 400 · 请检查服务"), active.ForDisplay(now.AddSeconds(4)) };
        for (var i = 0; i < states.Length; i++) Render(states[i], i, output, report);
    }

    private static void Render(TrafficSnapshot snapshot, int index, string output, List<string> report)
    {
        var peek = new AccountPeekWindow();
        peek.UpdateTraffic(snapshot, false);
        var shell = (Border)peek.Content;
        var body = (StackPanel)shell.Child;
        var text = string.Join("\n", body.Children.OfType<TextBlock>().Select(item => item.Text));
        if (!peek.IsTraffic || peek.AccountId is not null || !text.Contains(snapshot.TokenLabel + " Token") ||
            text.Contains("次") || (snapshot.Error is not null && !text.Contains(snapshot.Error)) ||
            body.Children.OfType<Grid>().Single().Children.Count != 12)
            throw new InvalidOperationException("流量悬停内容或未知状态错误");
        peek.Content = null;
        var root = new Canvas { Width = 400, Height = 320,
            Background = (Brush)Application.Current.FindResource("Surface") };
        var gauge = new QuotaGauge(); gauge.SetTraffic(snapshot);
        Canvas.SetLeft(gauge, 20); Canvas.SetTop(gauge, 20); root.Children.Add(gauge);
        shell.Width = 304; shell.Measure(new Size(304, 1000));
        if (shell.DesiredSize.Height >= 260) throw new InvalidOperationException("流量小卡过高");
        Canvas.SetLeft(shell, 65); Canvas.SetTop(shell, 90); root.Children.Add(shell);
        root.Measure(new Size(400, 320)); root.Arrange(new Rect(0, 0, 400, 320)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(400, 320, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, $"preview-traffic-hover-{index}.png")); encoder.Save(file);
        root.Children.Remove(shell); peek.Content = shell; peek.Close();
        report.Add("通过：流量悬停内容、紧凑尺寸与12格趋势：" + index);
    }
}
