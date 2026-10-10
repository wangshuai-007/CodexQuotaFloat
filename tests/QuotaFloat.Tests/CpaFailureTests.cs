using System.Net;
using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

static class CpaFailureTests
{
    /// <summary>验证异常响应不会崩溃、泄露凭据或假装满额。</summary>
    public static async Task<int> RunAsync()
    {
        var passed = 0;
        string[] bodies = [
            """{"status_code":401,"body":"secret-should-not-appear"}""",
            """{"status_code":"200","body":{}}""",
            """[]""",
            """{"status_code":200,"body":"not-json"}""",
            """{"status_code":200,"body":{"rate_limit":{}}}"""
        ];
        foreach (var body in bodies)
        {
            using var handler = new Handler(request => Json(request.Method == HttpMethod.Get ?
                """{"files":[{"auth_index":"a","provider":"codex","email":"demo@demo.local"}]}""" : body));
            using var client = new HttpClient(handler);
            var items = await new CpaQuotaSource(client, "http://127.0.0.1:8317/v8/management", "test-key").ReadAsync(default);
            if (items.Count != 1 || items[0].Error is null || items[0].Available(DateTimeOffset.Now) is not null ||
                items[0].Error!.Contains("secret")) throw new InvalidOperationException("失败：异常CPA响应处理");
            Console.WriteLine("通过：异常CPA响应保持错误行、不计入水位、不泄露正文");
            passed++;
        }
        foreach (var list in new[] { "[]", """{"files":[null]}""" })
        {
            using var handler = new Handler(_ => Json(list));
            using var client = new HttpClient(handler);
            try
            {
                await new CpaQuotaSource(client, "http://127.0.0.1:8317/v8/management", "test-key").ReadAsync(default);
                throw new InvalidOperationException("失败：无效账号列表未报错");
            }
            catch (FormatException) { Console.WriteLine("通过：无效账号列表明确格式错误"); passed++; }
        }
        using (var handler = new Handler(request => request.Method == HttpMethod.Get ?
            Json("""{"files":[{"auth_index":"a","provider":"codex"},{"auth_index":"a","provider":"codex"}]}""") :
            new HttpResponseMessage(HttpStatusCode.Forbidden)))
        using (var client = new HttpClient(handler))
        {
            try
            {
                await new CpaQuotaSource(client, "http://127.0.0.1:8317/v8/management", "test-key").ReadAsync(default);
                throw new InvalidOperationException("失败：管理代理鉴权错误未传播");
            }
            catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.Forbidden)
            { Console.WriteLine("通过：管理代理403传播到会话以暂停自动刷新"); passed++; }
        }
        using (var handler = new Handler(request => request.Method == HttpMethod.Get ?
            Json("""{"files":[{"auth_index":"a","provider":"codex"},{"auth_index":"a","provider":"codex"}]}""") :
            Json("""{"status_code":200,"body":{}}""")))
        using (var client = new HttpClient(handler))
        {
            var items = await new CpaQuotaSource(client, "http://127.0.0.1:8317/v8/management", "test-key").ReadAsync(default);
            if (items.Count != 1) throw new InvalidOperationException("失败：重复auth_index去重");
            Console.WriteLine("通过：重复auth_index只查询一次"); passed++;
        }
        return passed;
    }

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK)
        { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
