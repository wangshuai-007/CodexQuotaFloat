using System.Text.Json;
using System.Windows;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckRelativeAttachmentAsync(Window client, IntPtr handle, List<string> checks)
    {
        var independent = (settings.Left, settings.Top);
        coverageReader = _ => false;
        try
        {
            var initial = NativePlacement.Bounds(this);
            NativePlacement.Move(this, new Point(initial.Left - 110, initial.Top + 90));
            CompleteDrag();
            var dragged = NativePlacement.Bounds(this);
            await Task.Delay(650);
            var retained = NativePlacement.Bounds(this);
            Check(Math.Abs(retained.Left - dragged.Left) < 2 && Math.Abs(retained.Top - dragged.Top) < 2,
                "附着拖动后轮询不恢复默认位置，边缘停靠不覆盖相对位置", checks);
            Check((settings.Left, settings.Top) == independent && FollowOffset is not null,
                "附着拖动只保存相对偏移，不覆盖独立浮窗位置", checks);
            Check(ClientAttachment.TryReadOffset(this, handle, out var offset) &&
                (offset - FollowOffset!.Value).Length < 2, "相对偏移使用客户端DPI归一化", checks);
            var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            Check(restored.FollowOffsetX == settings.FollowOffsetX && restored.FollowOffsetY == settings.FollowOffsetY,
                "相对位置可序列化保存并恢复", checks);
            var origin = NativePlacement.Bounds(client);
            NativePlacement.Move(client, new Point(origin.Left + 45, origin.Top + 25));
            await Task.Delay(650);
            var moved = NativePlacement.Bounds(this);
            Check(Math.Abs(moved.Left - dragged.Left - 45) < 2 && Math.Abs(moved.Top - dragged.Top - 25) < 2,
                "拖动后的自定义位置随客户端移动，保持相对偏移", checks);
            settings.FollowCodex = false; ConfigureClientFollow();
            settings.FollowCodex = true; ConfigureClientFollow();
            var enabled = NativePlacement.Bounds(this);
            Check(Math.Abs(enabled.Left - moved.Left) < 2 && Math.Abs(enabled.Top - moved.Top) < 2,
                "关闭再开启跟随仍恢复自定义相对位置", checks);
        }
        finally
        {
            settings.FollowOffsetX = settings.FollowOffsetY = null;
            coverageReader = null;
            FollowClient();
        }
    }
}
