using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>独立Keeper认证与只读事件查询；绝不访问CPA队列或转发CPA密钥。</summary>
public sealed class KeeperTrafficSource : IDisposable
{
    private readonly HttpClient http;
    private readonly CookieContainer cookies = new();
    private readonly Uri root;
    private readonly string password;
    private bool authenticated;

    /// <summary>创建独立连接，不持久化密码或会话；可注入HTTP处理器进行离线验证。</summary>
    public KeeperTrafficSource(string address, string loginPassword, bool allowLanHttp, HttpMessageHandler? handler = null)
    {
        root = KeeperUrl.Validate(address, allowLanHttp);
        password = loginPassword;
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            { Timeout = TimeSpan.FromSeconds(4), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    }

    /// <summary>完整读取最近60秒事件，游标分页去除竞态；失败不返回部分统计。</summary>
    public async Task<JsonDocument> ReadAsync(CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(4));
        if (!authenticated) await AuthenticateAsync(budget.Token);
        var now = DateTimeOffset.UtcNow;
        var records = new List<KeeperRecord>();
        var cursor = "";
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        long responseBytes = 0;
        for (var page = 0; page < 200; page++)
        {
            using var request = Request(HttpMethod.Get, "usage/events?range=1h&page_size=100&cursor_mode=true" +
                (cursor.Length == 0 ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            using var response = await SendAsync(request, budget.Token);
            var payload = await response.Content.ReadAsByteArrayAsync(budget.Token);
            responseBytes += payload.Length;
            if (responseBytes > 4 * 1024 * 1024) throw new FormatException("Keeper近期记录超过4MiB，无法完整统计");
            using var data = JsonDocument.Parse(payload);
            var next = KeeperRecords.ReadPage(data.RootElement, now, records);
            if (next is null) return JsonSerializer.SerializeToDocument(new { records });
            if (!cursors.Add(next)) throw new FormatException("Keeper返回重复分页游标，统计已停止");
            cursor = next;
        }
        throw new FormatException("Keeper近期记录超过200页，无法完整统计");
    }

    private async Task AuthenticateAsync(CancellationToken token)
    {
        using var stateRequest = Request(HttpMethod.Get, "auth/session");
        using var stateResponse = await SendAsync(stateRequest, token);
        using var state = JsonDocument.Parse(await stateResponse.Content.ReadAsByteArrayAsync(token));
        if (!state.RootElement.TryGetProperty("authenticated", out var valid) ||
            valid.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new FormatException("Keeper登录状态格式不兼容，请检查应用根地址");
        if (valid.GetBoolean()) { authenticated = true; return; }
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Keeper需要登录密码，请在设置中输入，或设置KEEPER_LOGIN_PASSWORD");
        using var login = Request(HttpMethod.Post, "auth/login");
        login.Headers.Add("X-CPA-Usage-Keeper-Request", "fetch");
        login.Content = JsonContent.Create(new { password });
        using var response = await SendAsync(login, token);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
            foreach (var header in headers) cookies.SetCookies(root, header);
        if (!cookies.GetCookies(root).Cast<Cookie>().Any(cookie => cookie.Name == "cpa_usage_keeper_session" && !cookie.Expired))
            throw new FormatException("Keeper登录未返回有效会话Cookie，请检查部署路径与HTTPS设置");
        authenticated = true;
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(root, "api/v1/" + path));
        var cookie = cookies.GetCookies(request.RequestUri!).Cast<Cookie>()
            .FirstOrDefault(item => item.Name == "cpa_usage_keeper_session" && !item.Expired);
        if (cookie is not null) request.Headers.Add("Cookie", cookie.Name + "=" + cookie.Value);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var response = await http.SendAsync(request, token);
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        response.Dispose();
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) authenticated = false;
        throw new HttpRequestException("Keeper查询失败", null, status);
    }

    /// <summary>关闭本工具的HTTP连接；不退出用户浏览器或修改Keeper配置。</summary>
    public void Dispose() => http.Dispose();

    /// <summary>仅展示本地诊断文案，不展示HTTP响应正文、密码或Cookie。</summary>
    public static string SafeError(Exception error) => error switch
    {
        ArgumentException => error.Message,
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            "Keeper登录失败或会话失效 · 请检查Keeper密码；自动刷新已暂停",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Keeper接口不存在 · 请检查根地址及部署子路径",
        HttpRequestException { StatusCode: { } code } => $"Keeper HTTP {(int)code} · 请检查服务",
        HttpRequestException => "无法连接Keeper · 请检查地址和网络",
        FormatException => error.Message.StartsWith("Keeper", StringComparison.Ordinal) ? error.Message : "Keeper返回格式不兼容",
        JsonException or InvalidOperationException => "Keeper返回格式不兼容 · 请检查版本和地址",
        OperationCanceledException => "Keeper查询超过4秒 · 未使用不完整统计",
        _ => "Keeper查询失败 · 请手动刷新"
    };
}
