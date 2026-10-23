using QuotaFloat.Core;

internal static class MonthlyWaterTests
{
    internal static int Run()
    {
        var count = 0;
        void Check(string label, bool valid)
        {
            if (!valid) throw new InvalidOperationException("失败：" + label);
            Console.WriteLine("通过：" + label); count++;
        }
        var now = DateTimeOffset.UtcNow;
        var free = new AccountQuota("free", "free@demo.local", "free", null, null, null, null)
            { AdditionalWindows = [new(2592000, 75, now.AddDays(20))] };
        var paid = new AccountQuota("paid", "paid@demo.local", "Plus", 90, 40, now.AddHours(2), now.AddDays(3));
        foreach (var mode in Enum.GetValues<QuotaDisplayMode>())
        {
            var columns = WaterColumns.Build([free], now, mode);
            Check($"仅月账号所有设置均显示m：{mode}", columns is [{ Id: "m", Remaining: 75, AccountCount: 1 }]);
        }
        var mixed = WaterColumns.Build([paid, free], now, QuotaDisplayMode.Split);
        Check("混合账号三栏顺序5h/7d/m", mixed.Select(column => column.Id).SequenceEqual(new[] { "5h", "7d", "m" }));
        Check("三栏独立均值不混合月窗口", mixed.Select(column => column.Remaining).SequenceEqual(new double?[] { 90, 40, 75 }));
        Check("单5h设置自动追加月栏", WaterColumns.Build([paid, free], now, QuotaDisplayMode.FiveHour).Select(column => column.Id).SequenceEqual(new[] { "5h", "m" }));
        Check("单7d设置自动追加月栏", WaterColumns.Build([paid, free], now, QuotaDisplayMode.SevenDay).Select(column => column.Id).SequenceEqual(new[] { "7d", "m" }));
        Check("无月账号保留原双栏", WaterColumns.Build([paid], now, QuotaDisplayMode.Split).Count == 2);
        Check("Free缺窗口保留m但不伪造水位", WaterColumns.Build([free with { AdditionalWindows = [] }], now, QuotaDisplayMode.Split) is [{ Id: "m", Remaining: null }]);
        Check("月均值每账号等权", WaterColumns.Build([free, free with { Id = "free2", AdditionalWindows = [new(2592000, 25, now.AddDays(1))] }], now, QuotaDisplayMode.Split)[0].Remaining == 50);
        Check("月到期不计入水位", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [new(2592000, 75, now)] }, now) is null);
        Check("失败快照不计入月水位", WaterColumns.MonthlyRemaining(free with { Error = "失败" }, now) is null);
        Check("月窗口缺百分比不伪造零", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [new(2592000, null, now.AddDays(1))] }, now) is null);
        Check("月窗口非有限值不计入", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [new(2592000, double.NaN, now.AddDays(1))] }, now) is null);
        Check("不把其他周期折算月额度", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [new(900, 80, now.AddMinutes(5))] }, now) is null);
        Check("重复月窗口不重复计量", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [free.AdditionalWindows[0], free.AdditionalWindows[0]] }, now) is null);
        Check("未知重置保留已知月百分比", WaterColumns.MonthlyRemaining(free with { AdditionalWindows = [new(2592000, 75, null)] }, now) == 75);
        Check("空账号不显示假的月栏", WaterColumns.Build([], now, QuotaDisplayMode.Split).Count == 2);
        Check("月窗口过期能提示", (free with { AdditionalWindows = [new(2592000, 75, now)] }).IsExpired(now));
        Check("不同套餐的真实月窗口也可显示", WaterColumns.Build([free with { Plan = "Codex" }], now, QuotaDisplayMode.Split) is [{ Id: "m", Remaining: 75 }]);
        return count;
    }
}
