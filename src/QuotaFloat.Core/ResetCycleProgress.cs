namespace QuotaFloat.Core;

/// <summary>总体重置时间进度：优先7天，否则真实月窗口；有效账号等权平均。</summary>
public static class ResetCycleProgress
{
    /// <summary>失败或未知重置时间不计入；到期保持100%，不自动开启新周期。</summary>
    public static double? Calculate(IEnumerable<AccountQuota> accounts, DateTimeOffset now)
    {
        var values = accounts.Where(a => a.Error is null).Select(a =>
        {
            if (a.WeeklyReset is { } week) return QuotaMath.WeekElapsed(week, now);
            return MonthElapsed(a, now);
        }).OfType<double>().ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    /// <summary>账号月周期已过去的百分比；没有唯一有效重置时间时未知。</summary>
    public static double? MonthElapsed(AccountQuota account, DateTimeOffset now)
    {
        if (account.Error is not null) return null;
        var months = account.AdditionalWindows.Where(w => w.IsMonthly).Take(2).ToArray();
        return months.Length == 1 && months[0].Reset is { } reset
            ? Math.Clamp(100 * (1 - (reset - now).TotalSeconds / months[0].Seconds), 0, 100) : null;
    }
}
