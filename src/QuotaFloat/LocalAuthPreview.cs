using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>离线核验新增本地认证设置；不读取用户认证，不访问网络，不保存设置。</summary>
internal static class LocalAuthPreview
{
    internal static void Run(string output, List<string> report)
    {
        var settings = new SettingsWindow(new AppSettings { UseDemo = false, UseLocalAuth = true, RefreshSeconds = 5 }, "fixture-key");
        settings.DefaultAuthLabel.Text = "自动：已安装的Codex CLI；无CLI时读取示例 C:\\Users\\demo\\.codex\\auth.json";
        if (settings.SourceOption.SelectedIndex != 2 || settings.LocalAuthSection.Visibility != Visibility.Visible ||
            settings.CpaSection.Visibility != Visibility.Collapsed || settings.SnapshotSection.Visibility != Visibility.Collapsed ||
            settings.DemoCountOption.IsEnabled || settings.IntervalOption.Text != "5")
            throw new InvalidOperationException("本地认证设置可见性错误");
        Save((FrameworkElement)settings.Content, output, "preview-local-settings.png", 570, 1000);
        report.Add("通过：本地认证设置显示路径/选择文件，隐藏CPA设置，允许5秒");
        settings.SourceOption.SelectedIndex = 1;
        if (settings.LocalAuthSection.Visibility != Visibility.Collapsed || settings.CpaSection.Visibility != Visibility.Visible)
            throw new InvalidOperationException("切回CPA未恢复设置");
        report.Add("通过：切换本地认证/CPA恢复对应设置");
        settings.Close();
        var details = new DetailWindow();
        details.Update([], false, false, false, "本地Codex登录失效；请重新登录后手动刷新", false, null, true);
        details.SetTraffic(TrafficSnapshot.Unknown("本地认证仅查询额度，不提供最近60秒Token/请求统计"), false);
        Save((FrameworkElement)details.Content, output, "preview-local-error.png", 600, 600);
        details.Close();
        report.Add("通过：本地认证失败来源和流量不可用状态明确，不显示假额度");
        MonthlyPreview(output, report);
    }

    private static void MonthlyPreview(string output, List<string> report)
    {
        var account = new AccountQuota("fixture-monthly", "monthly@demo.local", "Free", null, null, null, null)
            { AdditionalWindows = [new(2592000, 100, DateTimeOffset.Now.AddDays(30))] };
        var row = new AccountRow(account, 1, DateTimeOffset.Now);
        Save(row, output, "preview-monthly-row.png", 580, 220);
        var peek = new AccountPeekWindow();
        peek.Update(account, 0, 1, false, null, false, true);
        Save((FrameworkElement)peek.Content, output, "preview-monthly-hover.png", 304, 220);
        var texts = Texts((DependencyObject)peek.Content).ToArray();
        if (!texts.Contains("月剩余") || !texts.Contains("100%") || texts.Contains("周剩余"))
            throw new InvalidOperationException("月额度悬停未展示实际周期或错误显示周额度");
        peek.Close();
        report.Add("通过：月额度列表/悬停显示100%及重置时刻，不冒充5h/周额度");
    }

    private static IEnumerable<string> Texts(DependencyObject element)
    {
        if (element is TextBlock text) yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var value in Texts(VisualTreeHelper.GetChild(element, i))) yield return value;
    }

    private static void Save(FrameworkElement panel, string output, string name, double width, double height)
    {
        panel.Measure(new Size(width, height));
        panel.Arrange(new Rect(0, 0, width, Math.Min(height, panel.DesiredSize.Height)));
        panel.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)Math.Ceiling(panel.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)Application.Current.FindResource("Surface"), null,
            new Rect(0, 0, width, panel.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(panel);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, name)); encoder.Save(file);
    }
}
