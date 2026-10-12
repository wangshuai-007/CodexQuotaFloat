using System.Net;
using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

internal static class LocalAuthTests
{
    private const string FakeToken = "fixture-local-access-not-real";
    internal static async Task<int> RunAsync()
    {
        var count = 0;
        void Check(string title, bool ok)
        {
            if (!ok) throw new InvalidOperationException("失败：" + title);
            Console.WriteLine("通过：" + title); count++;
        }
        CheckPathsAndParsing(Check);
        await LocalAuthEncodingTests.RunAsync(Check);
        await CheckRequestsAsync(Check);
        return count;
    }

    private static void CheckPathsAndParsing(Action<string, bool> Check)
    {
        var root = Path.GetTempPath();
        Check("默认用户.codex/auth.json", LocalCodexAuth.ResolvePath(null, "", root) == Path.Combine(root, ".codex", "auth.json"));
        Check("CODEX_HOME优先", LocalCodexAuth.ResolvePath(null, root, "ignored") == Path.Combine(root, "auth.json"));
        Check("显式auth文件优先", LocalCodexAuth.ResolvePath(Path.Combine(root, "chosen.json"), root) == Path.Combine(root, "chosen.json"));
        Check("用户目录缩写可用", LocalCodexAuth.ResolvePath("~/.codex/auth.json", "", root) == Path.Combine(root, ".codex", "auth.json"));
        foreach (var invalid in new[] { "relative/auth.json", "\\\\server\\share\\auth.json" })
        {
            try { LocalCodexAuth.ResolvePath(invalid, "", root); Check("拒绝非本地路径", false); }
            catch (ArgumentException) { Check("拒绝相对路径及网络认证文件", true); }
        }
        using var valid = JsonDocument.Parse(AuthJson());
        var parsed = LocalCodexAuth.Parse(valid.RootElement, DateTimeOffset.UtcNow);
        Check("认证对象不暴露凭据到JSON", JsonSerializer.Serialize(parsed) == "{}");
        foreach (var invalid in new[] { "{}", "[]", "{\"OPENAI_API_KEY\":\"fake-api\"}",
            "{\"auth_mode\":\"apikey\",\"tokens\":{\"access_token\":\"fake\",\"account_id\":\"fixture-account\"}}",
            AuthJson(account: ""), AuthJson(account: "bad\r\nheader"), AuthJson(access: "contains spaces"),
            AuthJson(access: Jwt(new { exp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds() })),
            AuthJson(access: "x.invalid-base64.x"), AuthJson(access: Jwt(new string[0])) })
        {
            try { using var bad = JsonDocument.Parse(invalid); LocalCodexAuth.Parse(bad.RootElement, DateTimeOffset.UtcNow); Check("拒绝不兼容本地认证", false); }
            catch (FormatException error) { Check("认证错误明确且无凭据泄露", !error.Message.Contains(FakeToken)); }
        }
        var claims = new Dictionary<string, object> { ["email"] = "local@demo.local",
            ["https://api.openai.com/auth"] = new { chatgpt_account_id = "fixture-account", chatgpt_plan_type = "plus" } };
        using var jwt = JsonDocument.Parse(AuthJson(account: null, id: Jwt(claims)));
        Check("ID令牌账号标识兼容", LocalCodexAuth.Parse(jwt.RootElement, DateTimeOffset.UtcNow) is not null);
        Check("读取失败不泄露路径", !LocalCodexQuotaSource.SafeError(new IOException("private-path")).Contains("private-path"));
    }

    private static async Task CheckRequestsAsync(Action<string, bool> Check)
    {
        using var file = new FixtureFile(AuthJson());
        var original = File.ReadAllBytes(file.PathName);
        using var handler = new LocalHandler(request =>
        {
            Check("本地认证仅发往固定官方HTTPS地址", request.Method == HttpMethod.Get && request.RequestUri!.AbsoluteUri == "https://chatgpt.com/backend-api/wham/usage");
            Check("凭据仅在Authorization和账号请求头", request.Headers.Authorization?.Parameter == FakeToken &&
                request.Headers.GetValues("ChatGPT-Account-Id").Single() == "fixture-account");
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"plan_type":"plus","rate_limit":{"primary_window":{"used_percent":20,"limit_window_seconds":18000,"reset_at":2000000000},"secondary_window":{"used_percent":55,"limit_window_seconds":604800,"reset_at":2000000000}}}""", Encoding.UTF8, "application/json") };
        });
        using var http = new HttpClient(handler);
        var source = new LocalCodexQuotaSource(http, file.PathName);
        var accounts = await source.ReadAsync(default);
        Check("本地额度单账号解析80/45", accounts.Count == 1 && accounts[0].FiveHourRemaining == 80 && accounts[0].WeeklyRemaining == 45);
        Check("账号模型不带访问凭据", !JsonSerializer.Serialize(accounts).Contains(FakeToken));
        Check("只读认证文件未改写", File.ReadAllBytes(file.PathName).SequenceEqual(original));
        File.WriteAllText(file.PathName, AuthJson(account: "fixture-next"), new UTF8Encoding(false));
        using var nextHttp = new HttpClient(new LocalHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }));
        var changed = await new LocalCodexQuotaSource(nextHttp, file.PathName).ReadAsync(default);
        Check("刷新重新读取换号认证", changed[0].Id == "local:fixture-next");
        await CheckInvalidResponsesAsync(file.PathName, Check);
        foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound })
        {
            using var failedHttp = new HttpClient(new LocalHandler(_ => new(code) { Content = new StringContent("secret-response-body") }));
            try { await new LocalCodexQuotaSource(failedHttp, file.PathName).ReadAsync(default); Check("HTTP错误传播", false); }
            catch (HttpRequestException error) { Check("鉴权错误明确且不显示响应正文", error.StatusCode == code && !LocalCodexQuotaSource.SafeError(error).Contains("secret-response-body")); }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await source.ReadAsync(cancellation.Token); Check("本地刷新可取消", false); }
        catch (OperationCanceledException) { Check("本地刷新可取消", true); }
    }

    private static async Task CheckInvalidResponsesAsync(string path, Action<string, bool> Check)
    {
        foreach (var (body, media, expected) in new[] {
            ("<html>private-response</html>", "text/html", "未返回JSON"),
            ("private-response", "application/json", "JSON无效"),
            ("[]", "application/json", "JSON结构不兼容") })
        {
            using var http = new HttpClient(new LocalHandler(_ => new(HttpStatusCode.OK) {
                Content = new StringContent(body, Encoding.UTF8, media) }));
            try { await new LocalCodexQuotaSource(http, path).ReadAsync(default); Check("拒绝错误额度响应", false); }
            catch (FormatException error)
            { Check("额度响应错误定位且不泄露正文", error.Message.Contains(expected) && !error.Message.Contains("private-response")); }
        }
        using var bomHttp = new HttpClient(new LocalHandler(_ => new(HttpStatusCode.OK) {
            Content = new StringContent("\ufeff{}", Encoding.UTF8, "application/json") }));
        Check("额度JSON兼容UTF8 BOM", (await new LocalCodexQuotaSource(bomHttp, path).ReadAsync(default)).Count == 1);
        using var timeoutHttp = new HttpClient(new LocalHandler(_ => throw new TaskCanceledException("private-timeout-body")));
        try { await new LocalCodexQuotaSource(timeoutHttp, path).ReadAsync(default); Check("HTTP超时独立定位", false); }
        catch (TimeoutException error)
        { Check("HTTP超时说明官方额度连接阶段且无正文", error.Message.StartsWith("连接官方额度接口超时") && !error.Message.Contains("private-timeout-body")); }
    }

    private static string AuthJson(string? account = "fixture-account", string access = FakeToken, string? id = null) =>
        JsonSerializer.Serialize(new { auth_mode = "chatgpt", tokens = new { access_token = access, account_id = account, id_token = id, refresh_token = "fake-unused-refresh" } });
    private static string Jwt(object claims) => "fixture." + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";

    private sealed class FixtureFile : IDisposable
    {
        internal string PathName { get; } = Path.Combine(Path.GetTempPath(), "QuotaFloatAuthTest-" + Guid.NewGuid() + ".json");
        internal FixtureFile(string json) => File.WriteAllText(PathName, json, new UTF8Encoding(false));
        public void Dispose() => File.Delete(PathName);
    }
    private sealed class LocalHandler(Func<HttpRequestMessage, HttpResponseMessage> read) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(read(request));
    }
}
