using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>按实际逻辑尺寸导出单账号悬停效果，避免大图掩盖浮窗尺寸。</summary>
internal static class HoverPreview
{
    internal static async Task RunAsync(string output, List<string> report)
    {
        var three = await new DemoQuotaSource(3).ReadAsync(default);
        for (var index = 0; index < 3; index++)
            Save(three, index, output, index == 0 ? "preview-hover.png" : $"preview-hover-{index + 1}.png");
        var twelve = await new DemoQuotaSource(12).ReadAsync(default);
        Save(twelve, 11, output, "preview-hover-scroll.png");
        report.Add("通过：三个鼠标位置分别渲染对应账号小卡");
        report.Add("通过：十二账号滚动后仅绘制十行，显示第十二账号");
    }

    private static void Save(IReadOnlyList<AccountQuota> accounts, int selected, string output, string file)
    {
        var now = DateTimeOffset.Now;
        var gauge = new QuotaGauge();
        gauge.Update(accounts, "演示", now, false, false);
        gauge.Scroll(selected);
        if (gauge.Width != 32 || gauge.Viewport.VisibleCount > 10 ||
            gauge.Viewport.Selected != selected)
            throw new InvalidOperationException("窄浮窗视口验证失败");
        var peek = new AccountPeekWindow();
        peek.Update(accounts[selected], selected, accounts.Count, true, null, false);
        var panel = (FrameworkElement)peek.Content;
        peek.Content = null;
        panel.Width = 304;
        panel.Measure(new Size(304, 1000));
        if (panel.DesiredSize.Height >= 230)
            throw new InvalidOperationException("单账号悬停卡高度超出约束");
        var root = new Canvas { Width = 400, Height = 280,
            Background = new LinearGradientBrush(Color.FromRgb(231, 238, 235), Color.FromRgb(247, 245, 239), 35) };
        Canvas.SetLeft(gauge, 24);
        Canvas.SetTop(gauge, 28);
        root.Children.Add(gauge);
        Canvas.SetLeft(panel, 72);
        Canvas.SetTop(panel, 48);
        root.Children.Add(panel);
        root.Measure(new Size(400, 280));
        root.Arrange(new Rect(0, 0, 400, 280));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(400, 280, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, file));
        encoder.Save(stream);
        root.Children.Remove(panel);
        peek.Content = panel;
        peek.Close();
    }
}
