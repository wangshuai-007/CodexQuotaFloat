namespace QuotaFloat.Core;

/// <summary>CPA最近60秒的已完成请求统计；未接入时数值未知，不能伪装成零。</summary>
public sealed record TrafficSnapshot(long? Tokens, int? Requests, IReadOnlyList<long> Buckets,
    DateTimeOffset? UpdatedAt, string? Error = null)
{
    /// <summary>统计来源，仅使用本地固定标签，不取服务端可变文本。</summary>
    public string SourceLabel { get; init; } = "CPA全局";
    /// <summary>本机session计量而非CPA已完成请求；不能按当前认证账号隔离。</summary>
    public bool IsLocalSession { get; init; }
    /// <summary>说明60秒窗口中记录的实际口径。</summary>
    public string WindowDescription => IsLocalSession ? "本地session计量增量" : "已完成请求用量";
    /// <summary>两次成功快照间新增Token除以实际秒数；首次采样或不可用时为未知。</summary>
    public double? TokensPerSecond { get; init; }
    /// <summary>两次成功快照间新增请求除以实际秒数；不是进行中请求数。</summary>
    public double? RequestsPerSecond { get; init; }
    /// <summary>实际成功采样间隔秒数，慢请求时不假装1秒。</summary>
    public double? SampleSeconds { get; init; }
    public string TokenRateLabel => RateLabel(TokensPerSecond);
    public string RequestRateLabel => RateLabel(RequestsPerSecond);

    /// <summary>最近60秒已完成请求Token总量除以固定60秒；无记录、异常或过期时未知。</summary>
    public double? AverageTokensPerSecond => Tokens is > 0 && UpdatedAt is not null && Error is null
        ? Tokens.Value / 60d : null;
    /// <summary>浮窗使用的60秒平均速率，不代表进行中请求的生成速度。</summary>
    public string AverageTokenRateLabel => RateLabel(AverageTokensPerSecond);
    /// <summary>平均流量的不可用原因，区分无记录与读取失败。</summary>
    public string AverageRateNotice => Error ?? (Tokens == 0 ? IsLocalSession ? "最近60秒无本地Token计量记录" : "最近60秒无已完成请求记录" :
        Tokens is null ? "等待只读统计快照" : "最近60秒" + WindowDescription + " ÷ 60秒");

    /// <summary>超过3秒未收到快照时停止显示旧瞬时速率，历史窗口仍保留截至时间。</summary>
    public TrafficSnapshot ForDisplay(DateTimeOffset now) => UpdatedAt is { } time && now - time > TimeSpan.FromSeconds(3)
        ? this with { TokensPerSecond = null, RequestsPerSecond = null, Error = "流量快照已超过3秒未更新" } : this;

    private static string RateLabel(double? value) => value switch
    {
        null => "—", >= 1_000_000 => $"{value / 1_000_000:0.#}m",
        >= 1_000 => $"{value / 1_000:0.#}k", _ => $"{value:0.#}"
    };

    public static TrafficSnapshot Unknown(string? error = null) => new(null, null, [], null, error);

    /// <summary>按千/百万缩写显示Token；保持单位在界面旁明确标注。</summary>
    public string TokenLabel => Tokens switch
    {
        null => "—",
        >= 1_000_000 => $"{Tokens / 1_000_000d:0.#}m",
        >= 1_000 => $"{Tokens / 1_000d:0.#}k",
        _ => $"{Tokens}"
    };
}
