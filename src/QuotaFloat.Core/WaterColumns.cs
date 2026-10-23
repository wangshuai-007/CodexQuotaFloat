namespace QuotaFloat.Core;

/// <summary>单个周期的独立水位；Id为5h、7d或m，NextReset为有效账号最近的重置时刻。</summary>
public sealed record WaterColumn(string Id, string Label, double? Remaining, int AccountCount, DateTimeOffset? NextReset);

/// <summary>按实际周期组合水位，月额度自动追加；仅月账号不展示不存在的5h/7天。</summary>
public static class WaterColumns
{
    /// <summary>构造至多5h/7d/m三列；mode：0=5小时；1=7天；2=左5小时、右7天。</summary>
    public static IReadOnlyList<WaterColumn> Build(IReadOnlyList<AccountQuota> accounts, DateTimeOffset now, QuotaDisplayMode mode)
    {
        var monthly = accounts.Any(HasMonth);
        var standard = !monthly || accounts.Any(account => account.FiveHourRemaining is not null ||
            account.WeeklyRemaining is not null || !HasMonth(account));
        var columns = new List<WaterColumn>();
        if (standard && mode != QuotaDisplayMode.SevenDay) columns.Add(Standard(accounts, now, false));
        if (standard && mode != QuotaDisplayMode.FiveHour) columns.Add(Standard(accounts, now, true));
        if (monthly) columns.Add(Month(accounts, now));
        return columns;
    }

    /// <summary>有真实月窗口或Free套餐时保留m栏；缺少实际窗口时仍显示未知，不伪造额度。</summary>
    public static bool HasMonth(AccountQuota account) => account.AdditionalWindows.Any(window => window.IsMonthly) ||
        string.Equals(account.Plan, "free", StringComparison.OrdinalIgnoreCase);

    /// <summary>每个账号最多计入一个月窗口；失败、到期及无效百分比不计入。</summary>
    public static double? MonthlyRemaining(AccountQuota account, DateTimeOffset now)
    {
        var windows = account.AdditionalWindows.Where(window => window.IsMonthly).ToArray();
        return account.Error is null && windows.Length == 1 && !(windows[0].Reset <= now) &&
            windows[0].Remaining is { } remaining && double.IsFinite(remaining) ? QuotaMath.Clamp(remaining) : null;
    }

    private static WaterColumn Month(IReadOnlyList<AccountQuota> accounts, DateTimeOffset now)
    {
        var valid = accounts.Where(account => MonthlyRemaining(account, now) is not null).ToArray();
        var values = valid.Select(account => MonthlyRemaining(account, now)!.Value).ToArray();
        return new("m", "月剩余", values.Length == 0 ? null : values.Average(), valid.Length,
            valid.SelectMany(account => account.AdditionalWindows).Where(window => window.IsMonthly).Select(window => window.Reset).Min());
    }

    private static WaterColumn Standard(IReadOnlyList<AccountQuota> accounts, DateTimeOffset now, bool weekly)
    {
        var mode = weekly ? QuotaDisplayMode.SevenDay : QuotaDisplayMode.FiveHour;
        var valid = accounts.Where(account => QuotaMath.WindowWaterLevel([account], now, mode) is not null).ToArray();
        return new(weekly ? "7d" : "5h", weekly ? "7天剩余" : "5h 剩余",
            QuotaMath.WindowWaterLevel(valid, now, mode), valid.Length,
            valid.Select(account => weekly ? account.WeeklyReset : account.FiveHourReset).Min());
    }
}
