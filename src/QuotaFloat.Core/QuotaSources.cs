namespace QuotaFloat.Core;

/// <summary>可替换的额度源；演示源与真实源永不自动相互回退。</summary>
public interface IQuotaSource
{
    /// <summary>读取全部账号，允许单个账号携带错误，支持取消。</summary>
    Task<IReadOnlyList<AccountQuota>> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>与用户示例百分比一致的匿名演示数据。</summary>
public sealed class DemoQuotaSource(int count = 3) : IQuotaSource
{
    /// <summary>创建动态重置时间但固定额度比例的演示账号，不访问网络。</summary>
    public Task<IReadOnlyList<AccountQuota>> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.Now;
        double[] five = [100, 100, 80, 62, 35, 97, 10, 83, 52, 73, 90, 44];
        double[] week = [88, 89, 30, 55, 21, 76, 6, 65, 48, 58, 81, 32];
        var items = Enumerable.Range(0, Math.Clamp(count, 0, 100)).Select(i => new AccountQuota(
            $"demo-{i + 1:00}", $"account-{i + 1:00}@demo.local", i == 2 || i % 4 == 3 ? "Plus" : "Team",
            five[i % five.Length], week[i % week.Length], now.AddHours(i == 2 ? 3 : 4),
            now.AddDays(i == 0 ? 5.95 : i == 1 ? 6.73 : i == 2 ? 4.7 : 1.1 + i % 5))).ToArray();
        return Task.FromResult<IReadOnlyList<AccountQuota>>(items);
    }
}
