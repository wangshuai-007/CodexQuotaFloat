using System.Windows;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class AccountPeekWindow
{
    /// <summary>当前小卡是否展示全部有效账号的总体水位。</summary>
    public bool IsWater { get; private set; }

    /// <summary>展示总体5h/7天/月剩余百分比，月窗口独立汇总。</summary>
    public void UpdateWater(IReadOnlyList<AccountQuota> values, DateTimeOffset now, string source, string? error)
    {
        IsWater = true;
        IsTraffic = false;
        AccountId = null;
        Title = "Codex 总体剩余额度";
        body.Children.Clear();
        body.Children.Add(new TextBlock { Text = "总体剩余额度", FontSize = 14, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = source + " · 有效账号等权平均", FontSize = 11,
            Foreground = Resource("Muted"), Margin = new Thickness(0, 3, 0, 9) });
        foreach (var column in WaterColumns.Build(values, now, QuotaDisplayMode.Split))
            Metric(column.Label, column.Remaining, column.Id == "m" ? "Cycle" : column.Id == "7d" ? "Week" : "Five",
                column.Id == "m" && column.NextReset is { } reset ? $"重置 {reset.LocalDateTime:MM/dd HH:mm}" : $"{column.AccountCount} 个有效账号");
        body.Children.Add(new TextBlock { Text = error ?? "各周期独立汇总 · 不计入失效或未知窗口",
            FontSize = 11, Foreground = Resource(error is null ? "Muted" : "Danger"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
    }
}
