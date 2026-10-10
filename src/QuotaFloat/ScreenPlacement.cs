using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuotaFloat;

/// <summary>当前显示器工作区和屏幕内定位；避免浮窗遮住任务栏。</summary>
public static class ScreenPlacement
{
    /// <summary>将当前屏幕物理工作区转换为当前WPF窗口使用的逻辑单位。</summary>
    public static Rect WorkArea(Window window)
    {
        var area = NativePlacement.WorkArea(window);
        var bounds = NativePlacement.Bounds(window);
        var scale = NativePlacement.Scale(window);
        // 仅把相对窗口的偏移换算为DIP，不缩放虚拟桌面的全局原点。
        return new Rect(window.Left + (area.Left - bounds.Left) / scale,
            window.Top + (area.Top - bounds.Top) / scale,
            area.Width / scale, area.Height / scale);
    }

    /// <summary>约束窗口完全可见，允许负坐标显示器。</summary>
    public static Point Clamp(Rect area, double width, double height, double left, double top) =>
        new(Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - width)),
            Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - height)));

    /// <summary>距任一工作区边缘28逻辑像素内时吸附，保持完整可见。</summary>
    public static Point Snap(Rect area, double width, double height, double left, double top)
    {
        var position = Clamp(area, width, height, left, top);
        if (position.X - area.Left < 28) position.X = area.Left;
        if (area.Right - position.X - width < 28) position.X = area.Right - width;
        if (position.Y - area.Top < 28) position.Y = area.Top;
        if (area.Bottom - position.Y - height < 28) position.Y = area.Bottom - height;
        return Clamp(area, width, height, position.X, position.Y);
    }

    /// <summary>详情向屏幕内展开；所有方向均限制在可用工作区。</summary>
    public static Point Detail(Rect area, Rect anchor, double width, double height)
    {
        var openLeft = anchor.Left + anchor.Width / 2 > area.Left + area.Width / 2;
        return Clamp(area, width, height, openLeft ? anchor.Left - width + 6 : anchor.Right - 6, anchor.Top - 10);
    }

    /// <summary>恢复坐标若完全离开现有屏幕则重置到当前工作区右侧。</summary>
    public static Point Restore(Window window, double? left, double? top)
    {
        var area = WorkArea(window);
        var dpi = VisualTreeHelper.GetDpi(window);
        var visible = left is { } x && top is { } y && System.Windows.Forms.Screen.AllScreens.Any(screen =>
            screen.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(
                (int)(x * dpi.DpiScaleX), (int)(y * dpi.DpiScaleY),
                (int)(window.Width * dpi.DpiScaleX), (int)(window.Height * dpi.DpiScaleY))));
        if (!visible) return new Point(area.Right - window.Width - 8, area.Top + Math.Max(8, (area.Height - window.Height) / 2));
        return new Point(left!.Value, top!.Value);
    }
}
