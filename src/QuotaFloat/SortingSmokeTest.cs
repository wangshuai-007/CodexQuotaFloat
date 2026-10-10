using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class MainWindow
{
    private async Task CheckSortingAndTrafficAsync(List<string> checks)
    {
        Expand(); details.ChangePage(1);
        var row = details.Rows.Children.OfType<AccountRow>().First();
        row.Focus(); await Task.Delay(40);
        var buttons = Descendants<Button>(row).ToArray();
        var thumb = Descendants<Thumb>(row).Single();
        Check(buttons.Length == 2 && buttons.All(b => b.IsVisible) && thumb.IsVisible,
            "账号行聚焦展示置顶、最后、拖动三个控件", checks);
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(DisplayAccounts[0].Id == "demo-11" && session.Accounts[0].Id == "demo-01", "第二页账号置顶，只改变本地顺序", checks);
        details.ChangePage(-1);
        row = details.Rows.Children.OfType<AccountRow>().First();
        Descendants<Button>(row).Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(DisplayAccounts[^1].Id == "demo-11", "账号最后按钮移动到全部账号末尾", checks);
        details.ChangePage(-1);
        row = details.Rows.Children.OfType<AccountRow>().ElementAt(1);
        thumb = Descendants<Thumb>(row).Single();
        thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        thumb.RaiseEvent(new DragDeltaEventArgs(0, row.ActualHeight) { RoutedEvent = Thumb.DragDeltaEvent });
        thumb.RaiseEvent(new DragCompletedEventArgs(0, row.ActualHeight, false) { RoutedEvent = Thumb.DragCompletedEvent });
        Check(DisplayAccounts[2].Id == "demo-02", "按住上下拖动事件路径调整账号顺序", checks);
        Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!
            .AccountOrder.SequenceEqual(settings.AccountOrder), "本地账号顺序可持久化恢复", checks);
        settings.AccountOrder.Clear(); UpdateViews(); Collapse();
        var unsupported = new AppSettings { UseDemo = false, ManagementUrl = "http://localhost:8317/v8/management" };
        traffic.Configure(unsupported, "test-key"); await traffic.RefreshAsync();
        Check(traffic.Snapshot.Tokens is null && traffic.Snapshot.Error!.Contains("只读"), "v8无快照明确未接入，不消费队列", checks);
        traffic.Configure(settings, ""); await traffic.RefreshAsync();
        Check(RefreshInterval.IsValid(5), "设置共享验证允许5秒刷新", checks);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
