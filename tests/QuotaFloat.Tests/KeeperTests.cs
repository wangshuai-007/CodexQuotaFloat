using System.Net;
using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

internal static class KeeperTests
{
    private static int passed;
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("失败：" + name);
        Console.WriteLine("通过：" + name); passed++;
    }

    internal static async Task<int> RunAsync()
    {
        ValidateUrls();
        await LoginAndPages();
        await Failures();
        await PaginationFailureAndCancellation();
        return passed;
    }

    private static void ValidateUrls()
    {
        foreach (var address in new[] { "http://192.168.50.226:8123", "http://public.example", "file:///tmp",
            "https://u:p@example.test", "https://example.test?password=x", "https://example.test/#x", "https://example.test/api/v1" })
        {
            try { KeeperUrl.Validate(address, false); Check("Keeper拒绝不安全根地址", false); }
            catch (ArgumentException) { Check("Keeper拒绝不安全根地址", true); }
        }
        Check("Keeper局域网HTTP须显式允许", KeeperUrl.Validate("http://192.168.50.226:8123", true).Port == 8123);
        Check("Keeper公网HTTP即使勾选也拒绝", Reject("http://8.8.8.8", true));
        Check("KeeperHTTPS保留部署子路径", KeeperUrl.Validate("https://example.test/keeper", false).AbsolutePath == "/keeper/");
        Check("Keeper根地址不是管理路径", KeeperUrl.Validate("http://127.0.0.1:8123", false).AbsolutePath == "/");
    }

    private static bool Reject(string address, bool allow)
    {
        try { KeeperUrl.Validate(address, allow); return false; }
        catch (ArgumentException) { return true; }
    }

    private static async Task LoginAndPages()
    {
        var now = DateTimeOffset.UtcNow;
        var calls = new List<string>();
        var page = 0;
        using var handler = new Handler(async request =>
        {
            var url = request.RequestUri!;
            calls.Add(request.Method + " " + url.PathAndQuery);
            Check("Keeper不携带CPA Authorization", request.Headers.Authorization is null);
            Check("Keeper请求限定用户服务子路径", url.Host == "example.test" && url.AbsolutePath.StartsWith("/keeper/api/v1/"));
            if (url.AbsolutePath.EndsWith("auth/session")) return Json("{\"authenticated\":false}");
            if (url.AbsolutePath.EndsWith("auth/login"))
            {
                Check("Keeper登录带请求意图头", request.Headers.GetValues("X-CPA-Usage-Keeper-Request").Single() == "fetch");
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Check("Keeper密码不裁剪空格", body.RootElement.GetProperty("password").GetString() == " fixture password ");
                var response = new HttpResponseMessage(HttpStatusCode.NoContent);
                response.Headers.Add("Set-Cookie", "cpa_usage_keeper_session=fixture-session; Path=/keeper; HttpOnly");
                return response;
            }
            Check("Keeper事件仅GET且携带会话Cookie", request.Method == HttpMethod.Get &&
                request.Headers.GetValues("Cookie").Single() == "cpa_usage_keeper_session=fixture-session");
            page++;
            return page == 1 ? Page(now, "1", 1000, true, "cursor+/") : Page(now.AddSeconds(-1), "2", 200, false);
        });
        using var source = new KeeperTrafficSource("https://example.test/keeper", " fixture password ", false, handler);
        using var data = await source.ReadAsync(default);
        var stats = new TrafficAccumulator(); stats.Ingest(data.RootElement, DateTimeOffset.UtcNow);
        Check("Keeper多页完整累计1200Token", stats.Snapshot(DateTimeOffset.UtcNow).Tokens == 1200);
        Check("Keeper游标安全编码", calls.Any(call => call.Contains("cursor=cursor%2B%2F")));
        using var again = await source.ReadAsync(default);
        stats.Ingest(again.RootElement, DateTimeOffset.UtcNow);
        Check("Keeper历史记录稳定ID去重", stats.Snapshot(DateTimeOffset.UtcNow).Tokens == 1200);
        Check("Keeper复用登录只登录一次", calls.Count(call => call.StartsWith("POST")) == 1);
        Check("Keeper没有队列配置或删除请求", calls.All(call => !call.Contains("queue") && !call.Contains("config") && !call.Contains("DELETE")));
    }

    private static async Task Failures()
    {
        using var anonymous = new KeeperTrafficSource("https://example.test", "", false,
            new Handler(_ => Task.FromResult(Json("{\"authenticated\":false}"))));
        try { using var data = await anonymous.ReadAsync(default); Check("Keeper缺密码明确失败", false); }
        catch (ArgumentException error) { Check("Keeper缺密码明确失败", error.Message.Contains("KEEPER_LOGIN_PASSWORD")); }
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Found })
        {
            using var source = new KeeperTrafficSource("https://example.test", "fixture", false,
                new Handler(_ => Task.FromResult(new HttpResponseMessage(status))));
            try { using var data = await source.ReadAsync(default); Check("KeeperHTTP失败传播", false); }
            catch (HttpRequestException error) { Check("KeeperHTTP失败传播", error.StatusCode == status); }
        }
        foreach (var payload in new[] { "{}", "{\"events\":[],\"has_more\":true}",
            "{\"events\":[{}],\"has_more\":false}", "<html>login</html>" })
        {
            using var source = new KeeperTrafficSource("https://example.test", "", false,
                new Handler(request => Task.FromResult(Json(request.RequestUri!.AbsolutePath.EndsWith("session") ?
                    "{\"authenticated\":true}" : payload))));
            try { using var data = await source.ReadAsync(default); Check("Keeper不完整数据不伪造零", false); }
            catch (Exception error) when (error is FormatException or JsonException)
            { Check("Keeper不完整数据不伪造零", true); }
        }
        Check("Keeper错误不暴露原响应/凭据", !KeeperTrafficSource.SafeError(new HttpRequestException("fixture secret", null,
            HttpStatusCode.Unauthorized)).Contains("fixture"));
    }

    private static HttpResponseMessage Page(DateTimeOffset time, string id, long tokens, bool more, string? cursor = null) =>
        Json(JsonSerializer.Serialize(new { events = new[] { new { id, timestamp = time, tokens = new { total_tokens = tokens } } },
            has_more = more, next_cursor = cursor }));

    private static async Task PaginationFailureAndCancellation()
    {
        var calls = 0;
        using var source = new KeeperTrafficSource("https://example.test", "", false,
            new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("session") ?
                Json("{\"authenticated\":true}") : ++calls == 1 ? Page(DateTimeOffset.UtcNow, "1", 100, true, "next") :
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        try { using var data = await source.ReadAsync(default); Check("Keeper中途失败不交付部分Token", false); }
        catch (HttpRequestException error) { Check("Keeper中途失败不交付部分Token", error.StatusCode == HttpStatusCode.ServiceUnavailable && calls == 2); }
        using var repeat = new KeeperTrafficSource("https://example.test", "", false,
            new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("session") ?
                Json("{\"authenticated\":true}") : Page(DateTimeOffset.UtcNow, "1", 100, true, "same"))));
        try { using var data = await repeat.ReadAsync(default); Check("Keeper重复游标不无限查询", false); }
        catch (FormatException error) { Check("Keeper重复游标不无限查询", error.Message.Contains("重复")); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var liveClient = new KeeperTrafficSource("http://127.0.0.1:1", "", false);
        try { using var data = await liveClient.ReadAsync(cancelled.Token); Check("Keeper取消可传播", false); }
        catch (OperationCanceledException) { Check("Keeper取消可传播", true); }
    }

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> call) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => call(request);
    }
}
