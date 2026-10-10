namespace QuotaFloat.Core;

/// <summary>CPA最近60秒的已完成请求统计；未接入时数值未知，不能伪装成零。</summary>
public sealed record TrafficSnapshot(long? Tokens, int? Requests, IReadOnlyList<long> Buckets,
    DateTimeOffset? UpdatedAt, string? Error = null)
{
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
