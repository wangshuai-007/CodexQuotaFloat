using System.Windows;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

public sealed partial class AccountPeekWindow
{
    /// <summary>当前悬停面板是否展示流量，而非账号详情。</summary>
    public bool IsTraffic { get; private set; }

    /// <summary>仅显示最近60秒Token及12个5秒趋势桶；未知不伪造为零。</summary>
    public void UpdateTraffic(TrafficSnapshot snapshot, bool demo)
    {
        IsTraffic = true;
        IsWater = false;
        AccountId = null;
        Title = "Codex 最近流量";
        body.Children.Clear();
        body.Children.Add(new TextBlock { Text = "最近 60 秒流量", FontSize = 14, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = snapshot.Tokens is null ? "流量未接入" : demo ? "演示流量" : snapshot.SourceLabel,
            FontSize = 11, Foreground = Resource("Muted"), Margin = new Thickness(0, 3, 0, 8) });
        body.Children.Add(new TextBlock { Text = snapshot.TokenLabel + " Token", FontSize = 24,
            FontWeight = FontWeights.SemiBold, Foreground = Resource("Five"),
            ToolTip = snapshot.Tokens is { } total ? $"{total:N0} Token" : "流量未接入" });
        body.Children.Add(TrafficTrend(snapshot));
        body.Children.Add(new TextBlock { Text = "每格 5 秒 · 左旧右新 · 已完成请求用量",
            FontSize = 11, Foreground = Resource("Muted"), Margin = new Thickness(0, 4, 0, 0) });
        var updated = snapshot.UpdatedAt is { } time ? $"截至 {time.LocalDateTime:HH:mm:ss}" : "尚未取得流量数据";
        body.Children.Add(new TextBlock { Text = snapshot.Error is { } error ? updated + " · " + error : updated,
            FontSize = 11, Foreground = Resource(snapshot.Error is null ? "Muted" : "Danger"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
    }

    private static Grid TrafficTrend(TrafficSnapshot snapshot)
    {
        var graph = new Grid { Height = 36, Margin = new Thickness(0, 8, 0, 0) };
        var max = Math.Max(1, snapshot.Buckets.DefaultIfEmpty(0).Max());
        for (var i = 0; i < 12; i++)
        {
            graph.ColumnDefinitions.Add(new ColumnDefinition());
            var value = i < snapshot.Buckets.Count ? snapshot.Buckets[i] : 0;
            var bar = new Border { Height = Math.Max(1, value / (double)max * 36),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 2, 0),
                CornerRadius = new CornerRadius(2, 2, 0, 0),
                Background = Resource(snapshot.Tokens is null ? "Stroke" : "Five"),
                ToolTip = snapshot.Tokens is null ? "未知" : $"{60 - i * 5}–{55 - i * 5}秒前 · {value:N0} Token" };
            Grid.SetColumn(bar, i); graph.Children.Add(bar);
        }
        return graph;
    }
}
