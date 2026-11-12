using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuotaFloat;

/// <summary>离线渲染关于页面，不发起更新检查或安装。</summary>
internal static class AboutPreview
{
    internal static void Run(string output, List<string> report)
    {
        var window = new AboutWindow();
        var panel = (FrameworkElement)window.Content;
        panel.Measure(new Size(500, 600));
        panel.Arrange(new Rect(0, 0, 500, panel.DesiredSize.Height));
        panel.UpdateLayout();
        var bitmap = new RenderTargetBitmap(500, (int)Math.Ceiling(panel.DesiredSize.Height), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle((Brush)Application.Current.FindResource("Surface"), null, new Rect(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Render(background); bitmap.Render(panel);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, "preview-about.png")); encoder.Save(file);
        window.Close();
        report.Add("通过：关于页面离线渲染、当前程序集版本与GitHub入口，不发起网络更新或安装");
    }
}