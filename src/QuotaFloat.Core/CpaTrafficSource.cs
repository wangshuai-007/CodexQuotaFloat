using System.Net.Http.Headers;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>只读流量快照；禁止消费队列，不自动改CPA配置或下载凭据。</summary>
public sealed class CpaTrafficSource(HttpClient client, string managementUrl, string key, string snapshotUrl)
{
    /// <summary>读取旧版usage或用户提供的同源只读快照；v8无配置时明确不支持。</summary>
    public async Task<JsonDocument> ReadAsync(CancellationToken token)
    {
        var url = ResolveUrl(managementUrl, snapshotUrl);
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("未配置CPA管理密钥");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }

    /// <summary>只向管理地址同源发送密钥，拒绝队列、重置和凭据下载路径。</summary>
    public static Uri ResolveUrl(string managementUrl, string snapshotUrl)
    {
        var root = CpaQuotaSource.ValidateUrl(managementUrl);
        if (string.IsNullOrWhiteSpace(snapshotUrl))
        {
            if (root.AbsolutePath.Contains("/v8/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("CPA v8无内置只读流量快照 · 请配置统计服务地址");
            return new Uri(root, "usage");
        }
        if (!Uri.TryCreate(snapshotUrl.Trim(), UriKind.Absolute, out var url) ||
            !string.Equals(url.Scheme, root.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(url.Authority, root.Authority, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            throw new ArgumentException("流量快照地址须与CPA管理地址同源，且不含用户名、查询参数或片段");
        var path = Uri.UnescapeDataString(url.AbsolutePath);
        if (new[] { "queue", "reset", "credentials", "auth-files", "download", "config" }
            .Any(part => path.Contains(part, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("只读流量不允许队列、重置、配置或凭据接口");
        return url;
    }
}
