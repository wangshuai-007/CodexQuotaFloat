using System.Net;
using System.Net.Http;

namespace QuotaFloat.Core;

/// <summary>仅保存本地生成的更新错误说明，不展示服务器原始错误正文。</summary>
public sealed class UpdateCheckFailure(string message) : FormatException(message)
{
    /// <summary>把更新错误转换为可操作提示，不泄露响应正文或下载地址参数。</summary>
    public static string Describe(Exception error) => error switch
    {
        UpdateCheckFailure known => known.Message,
        OperationCanceledException => "检查已取消或超时，请稍后重试。",
        HttpRequestException { StatusCode: { } code } => $"GitHub请求失败（HTTP {(int)code}），请稍后重试。",
        HttpRequestException => "无法连接GitHub，请检查网络或系统代理后重试。",
        FormatException => "发布信息不兼容或缺少安装包/校验值，请到GitHub Release查看。",
        _ => "无法解析GitHub发布信息，请到GitHub Release查看。"
    };

    internal static UpdateCheckFailure FromResponse(HttpResponseMessage response)
    {
        var code = response.StatusCode;
        if (code == HttpStatusCode.NotFound)
            return new("GitHub尚无可访问的正式Release；只有标签、草稿或Actions产物不能用于更新。");
        var limited = (int)code == 429 || code == HttpStatusCode.Forbidden &&
            response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0");
        if (!limited) return new($"GitHub请求被拒绝（HTTP {(int)code}），请检查网络或访问权限。");
        var retry = "";
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) &&
            long.TryParse(reset.FirstOrDefault(), out var unix) && unix is >= 0 and <= 253402300799)
            retry = $" 预计 {DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime:MM/dd HH:mm:ss} 后重试。";
        return new("GitHub API访问次数已用尽（HTTP " + (int)code + "）。" + retry +
            " 可先打开GitHub Release页面手动查看；重复点击不会解除限流。");
    }
}