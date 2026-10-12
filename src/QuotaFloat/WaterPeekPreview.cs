using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

internal static class WaterPeekPreview
{
    internal static async Task RunAsync(string output, List<string> report)
    {
        var values = await new DemoQuotaSource().ReadAsync(default);
        Render(values, null, 0, output, report);
        Render([], null, 1, output, report);
        Render(values, "额度刷新失败 · 旧数据", 2, output, report);
    }

    private static void Render(IReadOnlyList<AccountQuota> values, string? error, int index,
        string output, List<string> report)
    {
        var peek = new AccountPeekWindow();
        peek.UpdateWater(values, DateTimeOffset.Now, "演示额度", error);
        var shell = (Border)peek.Content;
        var body = (StackPanel)shell.Child;
        var text = string.Join(" ", body.Children.OfType<Grid>()
            .SelectMany(row => row.Children.OfType<TextBlock>()).Select(block => block.Text));
        if (!peek.IsWater || peek.IsTraffic || peek.AccountId is not null ||
            (index == 1 ? text.Contains("0%") || !text.Contains("—") : !text.Contains("93%") || !text.Contains("69%")) ||
            (error is not null && !body.Children.OfType<TextBlock>().Any(block => block.Text.Contains(error))))
            throw new InvalidOperationException("总体水位悬停百分比或未知状态错误");
        peek.Content = null;
        shell.Width = 304; shell.Measure(new Size(304, 1000));
        if (shell.DesiredSize.Height >= 230) throw new InvalidOperationException("总体水位卡过高");
        var canvas = new Canvas { Width = 340, Height = 240,
            Background = (Brush)Application.Current.FindResource("Surface") };
        Canvas.SetLeft(shell, 18); Canvas.SetTop(shell, 24); canvas.Children.Add(shell);
        canvas.Measure(new Size(340, 240)); canvas.Arrange(new Rect(0, 0, 340, 240));
        var bitmap = new RenderTargetBitmap(340, 240, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, $"preview-water-peek-{index}.png")); encoder.Save(stream);
        canvas.Children.Remove(shell); peek.Content = shell; peek.Close();
        report.Add("通过：总体水位悬停正确百分比/未知/错误与紧凑尺寸：" + index);
    }
}
