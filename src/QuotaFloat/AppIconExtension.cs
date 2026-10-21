using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace QuotaFloat;

/// <summary>保留ICO解码器及全部尺寸帧，供WPF创建原生大/小窗口图标。</summary>
[MarkupExtensionReturnType(typeof(BitmapFrame))]
public sealed class AppIconExtension : MarkupExtension
{
    private static readonly BitmapFrame image = Load();

    /// <summary>返回共享且冻结的图标帧，避免BitmapImage丢失多尺寸信息。</summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => image;

    private static BitmapFrame Load()
    {
        var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/Assets/App.ico"),
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(item => item.PixelWidth).First();
        frame.Freeze();
        return frame;
    }
}
