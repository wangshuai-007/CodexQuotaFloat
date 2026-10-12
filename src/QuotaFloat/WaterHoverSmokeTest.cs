using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckWaterHoverAsync(List<string> checks)
    {
        foreach (var mode in Enum.GetValues<QuotaDisplayMode>())
        {
            settings.QuotaDisplay = mode;
            UpdateViews(); ShowPeekAt(12);
            await Task.Delay(80);
            var body = (StackPanel)((Border)peek.Content).Child;
            var text = string.Join(" ", body.Children.OfType<Grid>()
                .SelectMany(row => row.Children.OfType<TextBlock>()).Select(block => block.Text));
            Check(peek.IsVisible && peek.IsWater && !peek.IsTraffic && !details.IsVisible,
                mode + " 模式水位标题悬停展示总体卡", checks);
            Check(text.Contains("93%") && text.Contains("69%"), mode + " 总体悬停显示正确5h/7天百分比", checks);
        }
        ShowPeekAt(50);
        Check(!peek.IsWater && peek.AccountId == "demo-01", "移回账号区恢复账号详情", checks);
        ShowPeekAt(230);
        Check(peek.IsTraffic && !peek.IsWater, "水位卡切换到最近流量卡", checks);
        ShowPeekAt(50);
    }
}
