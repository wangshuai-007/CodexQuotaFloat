namespace QuotaFloat.Core;

/// <summary>服务返回的实际窗口秒数、剩余百分比及重置时刻；不推断套餐或Token上限。</summary>
public sealed record AdditionalQuotaWindow(double Seconds, double? Remaining, DateTimeOffset? Reset)
{
    /// <summary>上游返回的30天月额度窗口，不按套餐名称推断百分比。</summary>
    public bool IsMonthly => Seconds == 2592000;
    /// <summary>按实际时长命名，30天明确标记月周期，不将其称为周额度。</summary>
    public string Label => Seconds == 2592000 ? "30天（月周期）" : Seconds % 86400 == 0 ?
        $"{Seconds / 86400:0}天" : Seconds % 3600 == 0 ? $"{Seconds / 3600:0}小时" : $"{Seconds / 60:0.#}分钟";
}
