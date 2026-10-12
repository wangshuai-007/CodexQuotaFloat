using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>使用本地ChatGPT认证直接查询固定官方HTTPS额度地址，不经过CPA。</summary>
public sealed class LocalCodexQuotaSource(HttpClient client, string? authPath) : IQuotaSource
{
    /// <summary>只读auth.json及额度；凭据不进入URL、响应错误或账号模型。</summary>
    public async Task<IReadOnlyList<AccountQuota>> ReadAsync(CancellationToken token)
    {
        var auth = await LocalCodexAuth.ReadAsync(authPath, token);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        request.Headers.Add("ChatGPT-Account-Id", auth.AccountId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("CodexQuotaFloat/0.2");
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("本地Codex额度查询失败", null, response.StatusCode);
        using var usage = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var account = CpaParser.Parse(usage.RootElement, "local:" + auth.AccountId, auth.Name, auth.Plan);
        return [account];
    }

    /// <summary>仅输出固定说明与HTTP状态，不输出路径、凭据或服务响应正文。</summary>
    public static string SafeError(Exception error) => error switch
    {
        FileNotFoundException or DirectoryNotFoundException => "未找到本地auth.json；请在Codex以ChatGPT登录，或选择认证文件。keyring认证不在文件中",
        UnauthorizedAccessException => "无法读取本地认证文件；请检查文件权限",
        IOException => "本地认证文件或Codex CLI暂时无法读取；请稍后手动刷新",
        System.ComponentModel.Win32Exception => "无法启动Codex CLI；请检查安装并更新CLI，或手动选择认证文件",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "本地Codex登录失效；请重新登录后手动刷新",
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "本地额度服务拒绝访问（403）；请检查登录、工作区权限或网络",
        HttpRequestException { StatusCode: { } code } => $"本地额度服务HTTP {(int)code}；请稍后刷新",
        HttpRequestException => "无法连接本地Codex额度服务；请检查网络或代理",
        OperationCanceledException => "本地额度查询超时；可手动刷新",
        FormatException => error.Message.StartsWith("CPA", StringComparison.Ordinal) ? "本地额度响应格式不兼容" : error.Message,
        JsonException => "本地认证或额度JSON格式无效；不显示错误正文",
        ArgumentException => "本地认证文件路径或请求参数无效；请检查设置",
        _ => "本地Codex额度读取失败"
    };
}
