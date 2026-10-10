using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>通过 CPA 管理 API 查询已登记 Codex 账号；不下载凭据、不改变账号状态。</summary>
public sealed class CpaQuotaSource(HttpClient client, string managementUrl, string managementKey) : IQuotaSource
{
    /// <summary>验证管理地址；只允许本机HTTP或远端HTTPS，不跟随重定向传送密钥。</summary>
    public static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("CPA 地址须为本机 HTTP 或远端 HTTPS 管理地址，不能带用户名、查询或片段。");
        if (!uri.AbsolutePath.TrimEnd('/').EndsWith("/management", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请填写完整管理地址，例如 http://127.0.0.1:8317/v8/management。");
        return uri;
    }

    /// <summary>读取全部启用的 Codex 账号，最多3个并发；单账号错误明确保留为错误行。</summary>
    public async Task<IReadOnlyList<AccountQuota>> ReadAsync(CancellationToken cancellationToken)
    {
        var root = ValidateUrl(managementUrl);
        if (string.IsNullOrWhiteSpace(managementKey)) throw new ArgumentException("请在设置中输入 CPA 管理密钥。");
        var modern = root.AbsolutePath.Contains("/v8/", StringComparison.OrdinalIgnoreCase);
        using var response = await RequestAsync(root, modern ? "credentials" : "auth-files", null, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            throw new FormatException("CPA 账号列表格式不兼容，应包含 files 数组。");
        if (files.EnumerateArray().Any(a => a.ValueKind != JsonValueKind.Object))
            throw new FormatException("CPA 账号列表包含无效记录。");
        var records = files.EnumerateArray().Where(IsCodex).Where(a => !Flag(a, "disabled"))
            .DistinctBy(a => CpaParser.Text(a, "auth_index") ?? CpaParser.Text(a, "name")).ToArray();
        using var concurrency = new SemaphoreSlim(3);
        var tasks = records.Select(async account =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try { return await ReadAccountAsync(root, modern, account, cancellationToken); }
            finally { concurrency.Release(); }
        });
        return await Task.WhenAll(tasks);
    }

    private async Task<AccountQuota> ReadAccountAsync(Uri root, bool modern, JsonElement account, CancellationToken token)
    {
        var id = CpaParser.Text(account, "auth_index") ?? "";
        var name = CpaParser.Text(account, "email") ?? CpaParser.Text(account, "name") ?? "未命名账号";
        var plan = CpaParser.Text(account, "plan_type") ?? "Codex";
        if (id.Length == 0 || Flag(account, "unavailable"))
            return new(id, name, plan, null, null, null, null, id.Length == 0 ? "缺少 auth_index" : "CPA 标记账号不可用");
        try
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer $TOKEN$",
                ["Accept"] = "application/json",
                ["User-Agent"] = "CodexQuotaFloat/0.1"
            };
            var accountId = CpaParser.Text(account, "account_id");
            if (account.TryGetProperty("id_token", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                accountId ??= CpaParser.Text(metadata, "chatgpt_account_id");
            if (!string.IsNullOrEmpty(accountId)) headers["ChatGPT-Account-Id"] = accountId;
            var request = new { auth_index = id, method = "GET",
                url = "https://chatgpt.com/backend-api/wham/usage", header = headers };
            using var response = await RequestAsync(root, modern ? "requests/api-call" : "api-call", request, token);
            using var wrapper = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var result = wrapper.RootElement;
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("status_code", out var code) ||
                code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status))
                throw new FormatException("CPA 代理响应缺少 status_code。");
            if (status is < 200 or >= 300)
                return new(id, name, plan, null, null, null, null, $"额度服务 HTTP {status} · 请检查账号");
            if (!result.TryGetProperty("body", out var body)) throw new FormatException("额度响应缺少 body。");
            if (body.ValueKind != JsonValueKind.String) return CpaParser.Parse(body, id, name, plan);
            using var usage = JsonDocument.Parse(body.GetString() ?? "{}");
            return CpaParser.Parse(usage.RootElement, id, name, plan);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return new(id, name, plan, null, null, null, null, "请求超时 · 可手动刷新");
        }
        catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or FormatException)
        {
            return new(id, name, plan, null, null, null, null, SafeError(error));
        }
    }

    private async Task<HttpResponseMessage> RequestAsync(Uri root, string path, object? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(root, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managementKey.Trim());
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException($"CPA HTTP {(int)status}", null, status);
        }
        return response;
    }

    private static bool IsCodex(JsonElement account) =>
        string.Equals(CpaParser.Text(account, "provider") ?? CpaParser.Text(account, "type"), "codex", StringComparison.OrdinalIgnoreCase) ||
        (CpaParser.Text(account, "name")?.StartsWith("codex-", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool Flag(JsonElement value, string field) =>
        value.TryGetProperty(field, out var flag) && flag.ValueKind == JsonValueKind.True;

    /// <summary>输出不含响应正文、密钥或请求地址的错误说明。</summary>
    public static string SafeError(Exception error) => error switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            "CPA 管理鉴权失败 · 请检查密钥；自动刷新已暂停",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            "CPA 管理接口不存在 · 请检查 v8/v0 地址或是否启用管理服务",
        HttpRequestException { StatusCode: { } code } => $"CPA HTTP {(int)code} · 请检查服务",
        HttpRequestException => "无法连接 CPA · 请检查地址和网络",
        JsonException or FormatException => "CPA 返回格式不兼容 · 请检查版本",
        OperationCanceledException => "CPA 请求超时 · 可手动刷新",
        ArgumentException => error.Message,
        _ => "读取额度失败 · 可手动刷新"
    };
}
