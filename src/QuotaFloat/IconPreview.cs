using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace QuotaFloat;

/// <summary>核验真实窗口HICON的绘制面积，不展示窗口、不修改系统图标缓存。</summary>
internal static class IconPreview
{
    internal static void Run(string output, List<string> report)
    {
        var image = Application.Current.FindResource("AppIcon") as BitmapFrame;
        if (image?.Decoder is not IconBitmapDecoder decoder ||
            !new[] { 16, 24, 32, 48, 64, 128, 256 }.All(size => decoder.Frames.Any(frame => frame.PixelWidth == size)))
            throw new InvalidOperationException("窗口图标未保留完整ICO尺寸");
        report.Add("通过：窗口图标保留16至256像素的ICO多尺寸解码器");
        var old = new BitmapImage(new Uri("pack://application:,,,/Assets/App.ico"));
        var before = Capture(old, Path.Combine(output, "icon-before.png"));
        var after = Capture(image, Path.Combine(output, "icon-after.png"));
        if (after < 26 || after < before)
            throw new InvalidOperationException("窗口原生大图标绘制面积不足");
        report.Add($"通过：真实窗口32px HICON有效宽度：旧加载{before}px，新加载{after}px");
    }

    private static int Capture(ImageSource image, string output)
    {
        var window = new Window { Icon = image, ShowInTaskbar = false, Width = 100, Height = 100 };
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var icon = SendMessage(hwnd, 0x007F, new IntPtr(1), IntPtr.Zero);
            if (icon == IntPtr.Zero) throw new InvalidOperationException("窗口缺少原生大图标");
            using var bitmap = new Drawing.Bitmap(32, 32);
            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.Clear(Drawing.Color.White);
                var dc = graphics.GetHdc();
                try
                {
                    if (!DrawIconEx(dc, 0, 0, icon, 32, 32, 0, IntPtr.Zero, 3))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { graphics.ReleaseHdc(dc); }
            }
            var left = 32;
            var right = -1;
            for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.R < 240 || color.G < 240 || color.B < 240)
                    { left = Math.Min(left, x); right = Math.Max(right, x); }
                }
            bitmap.Save(output, Drawing.Imaging.ImageFormat.Png);
            return Math.Max(0, right - left + 1);
        }
        finally { window.Close(); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height,
        uint step, IntPtr brush, uint flags);
}
