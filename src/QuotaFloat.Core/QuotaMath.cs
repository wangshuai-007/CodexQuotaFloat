namespace QuotaFloat.Core;

/// <summary>单个账号的额度快照；剩余百分比越高表示可用额度越多。</summary>
public sealed record AccountQuota(
    string Id, string Name, string Plan,
    double? FiveHourRemaining, double? WeeklyRemaining,
    DateTimeOffset? FiveHourReset, DateTimeOffset? WeeklyReset,
    string? Error = null)
{
    /// <summary>服务返回的非5h/7天窗口；仅供详情，不折算或计入双窗口水位。</summary>
    public IReadOnlyList<AdditionalQuotaWindow> AdditionalWindows { get; init; } = [];

    /// <summary>额度是否已过窗口重置时间；到期数据不能假装是新窗口。</summary>
    public bool IsExpired(DateTimeOffset now) =>
        FiveHourReset is { } five && five <= now || WeeklyReset is { } week && week <= now;

    /// <summary>返回双窗口的可用下限；未知、失败或到期时不计入水位。</summary>
    public double? Available(DateTimeOffset now) =>
        Error is null && !IsExpired(now) && FiveHourRemaining is { } five && WeeklyRemaining is { } week
            ? Math.Min(QuotaMath.Clamp(five), QuotaMath.Clamp(week)) : null;
}

/// <summary>浮窗计算口径，不推断套餐 token 上限。</summary>
public static class QuotaMath
{
    /// <summary>把合法有限百分比约束在 0–100。</summary>
    public static double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    /// <summary>根据周重置时间计算固定 7 天周期已走过的百分比，过期保持100%。</summary>
    public static double? WeekElapsed(DateTimeOffset? reset, DateTimeOffset now) => reset is { } end
        ? Math.Clamp(100 * (1 - (end - now).TotalDays / 7), 0, 100) : null;

    /// <summary>有效账号的等权平均可用下限；空数据或全部失败返回未知。</summary>
    public static double? WaterLevel(IEnumerable<AccountQuota> accounts, DateTimeOffset now)
    {
        var values = accounts.Select(a => a.Available(now)).OfType<double>().ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    /// <summary>单窗口有效账号的等权平均；只检查选定窗口到期，不受另一窗口影响。
    /// mode：0=5小时；1=7天；2=左右双水位（须分别传0、1计算）。</summary>
    public static double? WindowWaterLevel(IEnumerable<AccountQuota> accounts, DateTimeOffset now, QuotaDisplayMode mode)
    {
        if (mode is not (QuotaDisplayMode.FiveHour or QuotaDisplayMode.SevenDay))
            throw new ArgumentOutOfRangeException(nameof(mode));
        var values = accounts.Where(a => a.Error is null).Select(a =>
        {
            var reset = mode == QuotaDisplayMode.FiveHour ? a.FiveHourReset : a.WeeklyReset;
            var remaining = mode == QuotaDisplayMode.FiveHour ? a.FiveHourRemaining : a.WeeklyRemaining;
            return reset <= now || remaining is null || !double.IsFinite(remaining.Value)
                ? (double?)null : Clamp(remaining.Value);
        }).OfType<double>().ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    /// <summary>分页后每页最多10账号；水位始终按全部账号计算。</summary>
    public static IReadOnlyList<AccountQuota> Page(IReadOnlyList<AccountQuota> accounts, int page) =>
        accounts.Skip(Math.Clamp(page, 0, Math.Max(0, (accounts.Count - 1) / 10)) * 10).Take(10).ToArray();

    /// <summary>显示剩余重置时间；到期不自动滚入下一周。</summary>
    public static string Countdown(DateTimeOffset? reset, DateTimeOffset now)
    {
        if (reset is null) return "重置时间未知";
        var time = reset.Value - now;
        if (time <= TimeSpan.Zero) return "已到期 · 待刷新";
        if (time.TotalDays >= 1) return $"{(int)time.TotalDays}天 {time.Hours}小时后";
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}小时 {time.Minutes}分后";
        return $"{Math.Max(1, (int)Math.Ceiling(time.TotalMinutes))}分钟后";
    }
}
