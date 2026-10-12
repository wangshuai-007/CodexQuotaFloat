using System.Windows;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class DetailWindow
{
    private int UpdateWaterSummary(IReadOnlyList<AccountQuota> values, QuotaDisplayMode mode)
    {
        var five = QuotaMath.WindowWaterLevel(values, now, QuotaDisplayMode.FiveHour);
        var week = QuotaMath.WindowWaterLevel(values, now, QuotaDisplayMode.SevenDay);
        var fiveCount = values.Count(a => QuotaMath.WindowWaterLevel([a], now, QuotaDisplayMode.FiveHour) is not null);
        var weekCount = values.Count(a => QuotaMath.WindowWaterLevel([a], now, QuotaDisplayMode.SevenDay) is not null);
        var split = mode == QuotaDisplayMode.Split;
        var weekly = mode == QuotaDisplayMode.SevenDay;
        WaterTitle.Text = split ? "总体剩余 · 左5h / 右7天" : weekly ? "7天 总体剩余水位" : "5h 总体剩余水位";
        WaterLabel.Text = split ? $"{Label(five)}% / {Label(week)}%" : Label(weekly ? week : five);
        WaterLabel.FontSize = split ? 24 : 38;
        WaterUnit.Visibility = split ? Visibility.Collapsed : Visibility.Visible;
        CountLabel.Text = split ? $"{values.Count} 个账号 · 有效5h {fiveCount} / 7天 {weekCount}" :
            $"{values.Count} 个账号 · { (weekly ? weekCount : fiveCount)} 个计入水位";
        return split ? Math.Min(fiveCount, weekCount) : weekly ? weekCount : fiveCount;
    }

    private static string Label(double? value) => value is { } percent ? $"{percent:0}" : "—";
}
