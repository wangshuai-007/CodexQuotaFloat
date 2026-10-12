using System.Windows;

namespace QuotaFloat;

public partial class MainWindow
{
    private Point? FollowOffset => settings.FollowOffsetX is { } x && double.IsFinite(x) &&
        settings.FollowOffsetY is { } y && double.IsFinite(y) ? new Point(x, y) : null;

    private void CompleteDrag()
    {
        if (attachedClient != IntPtr.Zero)
        {
            if (ClientAttachment.TryReadOffset(this, attachedClient, out var offset))
            {
                settings.FollowOffsetX = offset.X;
                settings.FollowOffsetY = offset.Y;
                SavePosition();
                return;
            }
            AttachmentFailed();
            return;
        }
        var area = ScreenPlacement.WorkArea(this);
        var point = settings.DockToEdge ? ScreenPlacement.Snap(area, Width, Height, Left, Top) :
            ScreenPlacement.Clamp(area, Width, Height, Left, Top);
        Left = point.X;
        Top = point.Y;
        SavePosition();
    }
}
