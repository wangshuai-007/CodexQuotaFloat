using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuotaFloat;

internal static class FastGlyph
{
    internal static Geometry Bolt { get; } = CreateBolt();

    internal static StackPanel MenuHeader(string label)
    {
        // 图标放在文字旁，不占用 WPF 原有的菜单勾选列。
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new System.Windows.Shapes.Path
        {
            Data = Bolt, Fill = new SolidColorBrush(Color.FromRgb(32, 93, 109)),
            Width = 12, Height = 14, Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
        };
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static Geometry CreateBolt()
    {
        var bolt = Geometry.Parse("M 7.1,0 L 1,8.2 H 5.2 L 4.3,14 L 11,5.3 H 6.7 Z");
        bolt.Freeze();
        return bolt;
    }
}
