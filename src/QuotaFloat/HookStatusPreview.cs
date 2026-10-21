using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>离线渲染设置页信任状态，不查询CLI、不写入用户钩子。</summary>
internal static class HookStatusPreview
{
    internal static void Run(string output, List<string> report)
    {
        foreach (var status in new[]
        {
            new CodexHookStatus(true, "ready", "已信任、已启用。关联启动已就绪；新会话首次发送消息后检查启动。"),
            new CodexHookStatus(false, "modified", "启动钩子已修改，需要重新审核信任；进入 Codex CLI 的 /hooks，找到 CodexQuotaFloat。"),
            new CodexHookStatus(false, "unknown", "检测超时，信任状态未知；可点击重新检测。")
        })
        {
            var window = new SettingsWindow(new AppSettings { FollowCodex = true, UseDemo = true }, "fixture");
            window.ShowHookStatus(status);
            if (window.HookStatusSection.Visibility != (status.Ready ? Visibility.Collapsed : Visibility.Visible) || !window.HookCheckButton.IsEnabled ||
                window.HookStatusLabel.Text != status.Message)
                throw new InvalidOperationException("信任检测页状态错误");
            var panel = (FrameworkElement)window.Content;
            panel.Measure(new Size(570, 640));
            panel.Arrange(new Rect(0, 0, 570, 640)); panel.UpdateLayout();
            var bitmap = new RenderTargetBitmap(570, 640, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)Application.Current.FindResource("Surface"), null,
                new Rect(0, 0, 570, 640));
            bitmap.Render(background); bitmap.Render(panel);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, "preview-hook-" + status.Code + ".png")); encoder.Save(file);
            window.Close();
            report.Add("通过：设置页钩子状态、重新检测按钮可用：" + status.Code);
        }
    }
}
