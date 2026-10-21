using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>用应用真实WPF组件离线渲染预览；不冒充实屏鼠标或多显示器测试。</summary>
public static class PreviewHarness
{
    /// <summary>导出演示、十账号、分页、空数据和失败状态，并验证窗口几何及属性。</summary>
    public static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var report = new List<string>();
        IconPreview.Run(output, report);
        var now = DateTimeOffset.Now;
        foreach (var count in new[] { 3, 10, 12, 0 })
        {
            var accounts = await new DemoQuotaSource(count).ReadAsync(default);
            var details = new DetailWindow();
            details.FitToWorkArea(new Rect(0, 0, 1600, count >= 10 ? 1700 : 1000));
            details.Update(accounts, true, true, false, null, false, now);
            details.SetTraffic(new TrafficSnapshot(4310, 18, [], now), true);
            Assert(details.VisibleAccountCount == Math.Min(10, count), $"详情最多10行：{count}账号", report);
            Assert(details.Topmost, "置顶属性打开", report);
            details.Topmost = false;
            Assert(!details.Topmost, "置顶属性关闭", report);
            SaveScene(details, accounts, $"preview-{count}.png", output, null);
            if (count == 3)
            {
                foreach (var row in details.Rows.Children.OfType<AccountRow>()) row.ShowActionsForPreview();
                SaveScene(details, accounts, "preview-sort.png", output, null);
            }
            if (count == 12)
            {
                details.ChangePage(1);
                Assert(details.VisibleAccountCount == 2, "12账号第二页2行", report);
                SaveScene(details, accounts, "preview-page2.png", output, null);
            }
            details.Close();
        }
        var three = await new DemoQuotaSource().ReadAsync(default);
        var errorWindow = new DetailWindow();
        errorWindow.Update(three, false, false, false, "无法连接 CPA · 请检查地址和网络", false, now);
        SaveScene(errorWindow, three, "preview-error.png", output, "连接失败");
        errorWindow.Close();
        var loadingWindow = new DetailWindow();
        loadingWindow.Update(three, false, false, false, null, true, now);
        SaveScene(loadingWindow, three, "preview-loading.png", output, "读取中");
        loadingWindow.Close();
        var compact = new DetailWindow();
        var ten = await new DemoQuotaSource(10).ReadAsync(default);
        compact.FitToWorkArea(new Rect(0, 0, 1280, 720));
        compact.Update(ten, true, true, false, null, false, now);
        var compactPanel = (FrameworkElement)compact.Content;
        compactPanel.Measure(new Size(600, 2000));
        Assert(compactPanel.DesiredSize.Height <= 720, "720像素工作区详情不裁切底部操作", report);
        SaveScene(compact, ten, "preview-compact10.png", output, null);
        compact.Close();
        await HoverPreview.RunAsync(output, report);
        LocalAuthPreview.Run(output, report);
        HookStatusPreview.Run(output, report);
        await WaterDisplayPreview.RunAsync(output, report);
        WaterDesignPreview.Run(output, report);
        await WaterPeekPreview.RunAsync(output, report);
        TrafficRatePreview.Run(output, report);
        TrafficPeekPreview.Run(output, report);
        KeeperPreview.Run(output, report);
        CodexFollowPreview.Run(report);
        CheckGeometry(report);
        File.WriteAllLines(Path.Combine(output, "ui-checks.txt"), report.Append(
            "限制：离线WPF渲染及属性/几何验证，不替代真实鼠标悬停、系统置顶或多屏混合DPI人工验收。"), new UTF8Encoding(false));
    }

    private static void CheckGeometry(List<string> report)
    {
        var area = new Rect(-1920, 0, 1920, 1040);
        var snapped = ScreenPlacement.Snap(area, 112, 320, -1911, 450);
        Assert(snapped.X == area.Left, "负坐标屏幕左侧吸附", report);
        snapped = ScreenPlacement.Snap(area, 112, 320, -118, 450);
        Assert(snapped.X == -112, "右侧吸附", report);
        snapped = ScreenPlacement.Snap(area, 112, 320, -800, 10);
        Assert(snapped.Y == 0, "上侧吸附", report);
        snapped = ScreenPlacement.Snap(area, 112, 320, -800, 710);
        Assert(snapped.Y == 720, "下侧吸附", report);
        var left = ScreenPlacement.Detail(area, new Rect(-1920, 100, 112, 320), 600, 650);
        Assert(left.X > -1920 && area.Contains(new Rect(left.X, left.Y, 600, 650)), "左边停靠向内展开", report);
        var right = ScreenPlacement.Detail(area, new Rect(-112, 800, 112, 320), 600, 650);
        Assert(right.X < -112 && area.Contains(new Rect(right.X, right.Y, 600, 650)), "右边停靠向内展开且不越任务栏", report);
        var clamped = ScreenPlacement.Clamp(area, 112, 320, 100, 2000);
        Assert(clamped.X == -112 && clamped.Y == 720, "越界位置限制", report);
    }

    private static void SaveScene(DetailWindow details, IReadOnlyList<AccountQuota> accounts, string file, string output, string? state)
    {
        var panel = (FrameworkElement)details.Content;
        details.Content = null;
        panel.Width = details.Width;
        panel.Measure(new Size(details.Width, 2000));
        var height = panel.DesiredSize.Height;
        var root = new Canvas { Width = 792, Height = Math.Max(430, height + 100),
            Background = new LinearGradientBrush(Color.FromRgb(231, 238, 235), Color.FromRgb(247, 245, 239), 35) };
        var title = new TextBlock { Text = "Codex 额度浮窗 / 原生 WPF 演示", FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(78, 98, 99)) };
        Canvas.SetLeft(title, 28); Canvas.SetTop(title, 24); root.Children.Add(title);
        var gauge = new QuotaGauge();
        gauge.Update(accounts, state ?? "演示", DateTimeOffset.Now, state == "连接失败", false);
        if (state is null) gauge.SetTraffic(new TrafficSnapshot(4310, 18, [0, 320, 170, 650, 120, 0, 930, 440, 170, 780, 220, 510], DateTimeOffset.Now));
        Canvas.SetLeft(gauge, 30); Canvas.SetTop(gauge, 100); root.Children.Add(gauge);
        Canvas.SetLeft(panel, 158); Canvas.SetTop(panel, 58); root.Children.Add(panel);
        root.Measure(new Size(root.Width, root.Height));
        root.Arrange(new Rect(0, 0, root.Width, root.Height));
        root.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(root.Width * 1.5), (int)Math.Ceiling(root.Height * 1.5),
            144, 144, PixelFormats.Pbgra32);
        image.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(output, file));
        encoder.Save(stream);
        root.Children.Remove(panel);
        details.Content = panel;
    }

    private static void Assert(bool condition, string label, List<string> report)
    {
        if (!condition) throw new InvalidOperationException("UI验证失败：" + label);
        report.Add("通过：" + label);
    }
}
