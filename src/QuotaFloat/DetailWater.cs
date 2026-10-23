using System.Windows;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class DetailWindow
{
    private int UpdateWaterSummary(IReadOnlyList<AccountQuota> values, QuotaDisplayMode mode)
    {
        var columns = WaterColumns.Build(values, now, mode);
        var split = columns.Count > 1;
        WaterTitle.Text = split ? "总体剩余 · " + string.Join(" / ", columns.Select(column => column.Id)) :
            columns[0].Id == "m" ? "月额度 总体剩余水位" : columns[0].Id == "7d" ? "7天 总体剩余水位" : "5h 总体剩余水位";
        WaterLabel.Text = split ? string.Join(" / ", columns.Select(column => Label(column.Remaining) + "%")) : Label(columns[0].Remaining);
        WaterLabel.FontSize = columns.Count == 3 ? 22 : split ? 24 : 38;
        WaterUnit.Visibility = split ? Visibility.Collapsed : Visibility.Visible;
        CountLabel.Text = "有效窗口 · " + string.Join(" / ", columns.Select(column => $"{column.Id} {column.AccountCount}"));
        return values.Count(account => WaterColumns.Build([account], now, mode).Any(column => column.Remaining is not null));
    }

    private static string Label(double? value) => value is { } percent ? $"{percent:0}" : "—";
}
