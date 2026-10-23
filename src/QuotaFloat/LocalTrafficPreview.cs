using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>离线核验本地认证默认流量和显式Keeper优先；仅使用匿名临时session。</summary>
internal static class LocalTrafficPreview
{
    internal static void Run(string output, List<string> report)
    {
        var snapshot = Task.Run(CheckSessionAsync).GetAwaiter().GetResult();
        var peek = new AccountPeekWindow();
        peek.UpdateTraffic(snapshot, false);
        var texts = Texts(peek.Content as System.Windows.DependencyObject).ToArray();
        if (!texts.Contains("本地session · 本机全部会话") || !texts.Any(text => text.Contains("本地session计量增量")))
            throw new InvalidOperationException("本地流量悬停未标明本地口径");
        peek.Close();
        report.Add("通过：本地认证默认session统计、重新配置清除旧数据、显式Keeper优先与本地流量悬停口径");
    }

    private static async Task<TrafficSnapshot> CheckSessionAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "quota-traffic-preview-" + Guid.NewGuid().ToString("N"));
        var sessions = Path.Combine(root, "sessions"); Directory.CreateDirectory(sessions);
        var previous = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", root);
            var record = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, type = "event_msg",
                payload = new { type = "token_count", info = new { total_token_usage = new { total_tokens = 6000 },
                    last_token_usage = new { total_tokens = 6000 } } } });
            await File.WriteAllTextAsync(Path.Combine(sessions, "rollout-demo.jsonl"), record + "\n");
            var options = new AppSettings { UseDemo = false, UseLocalAuth = true };
            using var session = new TrafficSession(options, "");
            await session.RefreshAsync();
            var snapshot = session.Snapshot;
            if (!snapshot.IsLocalSession || snapshot.Tokens != 6000 || snapshot.AverageTokensPerSecond != 100)
                throw new InvalidOperationException("本地默认session来源错误");
            session.Configure(new AppSettings { UseDemo = false, UseLocalAuth = true, UseKeeper = true, KeeperUrl = "invalid" }, "");
            if (session.Snapshot.Tokens is not null) throw new InvalidOperationException("切换来源沿用旧流量");
            await session.RefreshAsync();
            if (session.Snapshot.IsLocalSession || session.Snapshot.Error is null || session.Snapshot.Tokens is not null)
                throw new InvalidOperationException("显式Keeper失败时错误回退到本地");
            session.Configure(options, ""); await session.RefreshAsync();
            if (!session.Snapshot.IsLocalSession || session.Snapshot.Tokens != 6000)
                throw new InvalidOperationException("切回本地流量未恢复");
            return snapshot;
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", previous);
            Directory.Delete(root, true);
        }
    }

    private static IEnumerable<string> Texts(System.Windows.DependencyObject? node)
    {
        if (node is null) yield break;
        if (node is TextBlock text) yield return text.Text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var value in Texts(VisualTreeHelper.GetChild(node, i))) yield return value;
    }
}
