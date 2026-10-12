namespace QuotaFloat.Core;

/// <summary>水位显示方式：0=5小时额度；1=7天额度；2=左右同时显示5小时和7天额度。</summary>
public enum QuotaDisplayMode
{
    /// <summary>5小时额度，值为0。</summary>
    FiveHour = 0,
    /// <summary>7天额度，值为1。</summary>
    SevenDay = 1,
    /// <summary>左5小时、右7天双水位，值为2。</summary>
    Split = 2
}
