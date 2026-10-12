using QuotaFloat.Core;

internal static class WaterDisplayTests
{
    internal static int Run()
    {
        var passed = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException("失败：" + name);
            Console.WriteLine("通过：" + name);
            passed++;
        }
        var now = DateTimeOffset.UtcNow;
        var account = new AccountQuota("test", "演示", "Team", 80, 30, now.AddHours(4), now.AddDays(4));
        double? Level(AccountQuota[] accounts, QuotaDisplayMode mode) => QuotaMath.WindowWaterLevel(accounts, now, mode);
        var three = new[] { account with { FiveHourRemaining = 100, WeeklyRemaining = 88 },
            account with { FiveHourRemaining = 100, WeeklyRemaining = 89 }, account };
        Check("5h全部账号平均为93.33%", Math.Abs(Level(three, QuotaDisplayMode.FiveHour)!.Value - 280d / 3) < 0.001);
        Check("7天全部账号平均为69%", Level(three, QuotaDisplayMode.SevenDay) == 69);
        Check("5h到期不影响7天水位", Level([account with { FiveHourReset = now }], QuotaDisplayMode.SevenDay) == 30);
        Check("周到期不影响5h水位", Level([account with { WeeklyReset = now }], QuotaDisplayMode.FiveHour) == 80);
        Check("选定窗口到期不计入", Level([account with { FiveHourReset = now }], QuotaDisplayMode.FiveHour) is null);
        Check("另一窗口缺失仍计算5h", Level([account with { WeeklyRemaining = null }], QuotaDisplayMode.FiveHour) == 80);
        Check("另一窗口缺失仍计算7天", Level([account with { FiveHourRemaining = null }], QuotaDisplayMode.SevenDay) == 30);
        Check("选定窗口缺失为未知", Level([account with { WeeklyRemaining = null }], QuotaDisplayMode.SevenDay) is null);
        Check("空账号水位未知", Level([], QuotaDisplayMode.FiveHour) is null);
        Check("失败账号不计入", Level([account with { Error = "失败" }], QuotaDisplayMode.FiveHour) is null);
        Check("NaN不伪造零", Level([account with { FiveHourRemaining = double.NaN }], QuotaDisplayMode.FiveHour) is null);
        Check("无限值不计入", Level([account with { WeeklyRemaining = double.PositiveInfinity }], QuotaDisplayMode.SevenDay) is null);
        Check("负值夹紧为零", Level([account with { FiveHourRemaining = -10 }], QuotaDisplayMode.FiveHour) == 0);
        Check("溢出值夹紧为100", Level([account with { WeeklyRemaining = 110 }], QuotaDisplayMode.SevenDay) == 100);
        Check("部分未知只平均有效账号", Level([account, account with { WeeklyRemaining = null }], QuotaDisplayMode.SevenDay) == 30);
        try { Level(three, QuotaDisplayMode.Split); throw new InvalidOperationException("未拒绝双水位单值计算"); }
        catch (ArgumentOutOfRangeException) { Check("双水位须独立计算两个窗口", true); }
        return passed;
    }
}
