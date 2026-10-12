using System.Windows;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class AccountPeekWindow
{
    /// <summary>当前小卡是否展示全部有效账号的总体水位。</summary>
    public bool IsWater { get; private set; }

    /// <summary>展示总体5h/7天剩余百分比，供所有水位模式悬停查看。</summary>
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
        var five = QuotaMath.WindowWaterLevel(values, now, QuotaDisplayMode.FiveHour);
        var week = QuotaMath.WindowWaterLevel(values, now, QuotaDisplayMode.SevenDay);
        var fiveCount = values.Count(value => QuotaMath.WindowWaterLevel([value], now, QuotaDisplayMode.FiveHour) is not null);
        var weekCount = values.Count(value => QuotaMath.WindowWaterLevel([value], now, QuotaDisplayMode.SevenDay) is not null);
        Metric("5h 剩余", five, "Five", $"{fiveCount} 个有效账号");
        Metric("7天剩余", week, "Week", $"{weekCount} 个有效账号");
        body.Children.Add(new TextBlock { Text = error ?? "左5h / 右7天 · 不计入失效或未知窗口",
            FontSize = 11, Foreground = Resource(error is null ? "Muted" : "Danger"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
    }
}
