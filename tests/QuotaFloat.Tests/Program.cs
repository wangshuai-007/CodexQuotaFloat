using System.Net;
using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

if (args.SequenceEqual(new[] { "app-server" })) { await FakeCodexServer.RunAsync(); return; }
if (args.Contains("--check-local-auth")) { await LocalAuthLiveCheck.RunAsync(); return; }

var now = new DateTimeOffset(2026, 10, 10, 16, 30, 0, TimeSpan.FromHours(8));
var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException($"失败：{name}");
    Console.WriteLine($"通过：{name}");
    passed++;
}
void Near(string name, double? actual, double expected) =>
    Check(name, actual.HasValue && Math.Abs(actual.Value - expected) < 0.0001);
AccountQuota Item(double five, double week) => new("1", "演示账号", "Team", five, week, now.AddHours(4), now.AddDays(5));
var three = new[] { Item(100, 88), Item(100, 89), Item(80, 30) };
Near("示例水位为69%", QuotaMath.WaterLevel(three, now), 69);
Near("周期刚开始为0%", QuotaMath.WeekElapsed(now.AddDays(7), now), 0);
Near("周期中点为50%", QuotaMath.WeekElapsed(now.AddDays(3.5), now), 50);
Near("周期结束为100%", QuotaMath.WeekElapsed(now, now), 100);
Near("到期周期保持100%而非自动滚动", QuotaMath.WeekElapsed(now.AddDays(-1), now), 100);
Near("周期开始前夹紧为0%", QuotaMath.WeekElapsed(now.AddDays(8), now), 0);
Check("未知重置时间不伪造进度", QuotaMath.WeekElapsed(null, now) is null);
Check("空数据水位未知", QuotaMath.WaterLevel([], now) is null);
Check("缺失周数据不计入水位", QuotaMath.WaterLevel([Item(80, 30) with { WeeklyRemaining = null }], now) is null);
Check("全部失败水位未知", QuotaMath.WaterLevel([Item(80, 30) with { Error = "失败" }], now) is null);
Check("到期数据不赠予额度", QuotaMath.WaterLevel([Item(80, 30) with { FiveHourReset = now }], now) is null);
Near("部分失败不污染有效账号水位", QuotaMath.WaterLevel([Item(80, 30), Item(100, 90) with { Error = "失败" }], now), 30);
Near("负百分比限制为0", Item(-5, 80).Available(now), 0);
Near("溢出百分比限制为100", Item(110, 102).Available(now), 100);
Check("非有限百分比保守为0", QuotaMath.Clamp(double.NaN) == 0);
Check("未知重置时间说明", QuotaMath.Countdown(null, now) == "重置时间未知");
Check("到期重置时间说明", QuotaMath.Countdown(now, now).Contains("待刷新"));
var twelve = await new DemoQuotaSource(12).ReadAsync(default);
Check("第一页最多10个账号", QuotaMath.Page(twelve, 0).Count == 10);
Check("第二页2账号且无重复", QuotaMath.Page(twelve, 1).Count == 2 && QuotaMath.Page(twelve, 1)[0].Id == "demo-11");
Check("负页码夹紧", QuotaMath.Page(twelve, -1)[0].Id == "demo-01");
Check("超出页码夹紧", QuotaMath.Page(twelve, 100).Count == 2);
Check("空分页不崩溃", QuotaMath.Page([], 100).Count == 0);
var demo = await new DemoQuotaSource().ReadAsync(default);
Near("动态演示数据同样69%", QuotaMath.WaterLevel(demo, DateTimeOffset.Now), 69);
Check("演示邮箱不使用用户个人信息", demo.All(a => a.Name.EndsWith("@demo.local")));
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await new DemoQuotaSource().ReadAsync(cancelled.Token); Check("取消被传播", false); }
    catch (OperationCanceledException) { Check("取消被传播", true); }
}

string Window(double used, int duration) =>
    JsonSerializer.Serialize(new { used_percent = used, limit_window_seconds = duration,
        reset_at = now.AddDays(duration == 604800 ? 5 : 0.2).ToUnixTimeSeconds() });
string Usage(bool swapped = false) =>
    $"{{\"plan_type\":\"team\",\"rate_limit\":{{\"primary_window\":{Window(swapped ? 12 : 0, swapped ? 604800 : 18000)},\"secondary_window\":{Window(swapped ? 0 : 12, swapped ? 18000 : 604800)}}}}}";
foreach (var swapped in new[] { false, true })
{
    using var json = JsonDocument.Parse(Usage(swapped));
    var item = CpaParser.Parse(json.RootElement, "x", "匿名", "Codex");
    Near($"使用量转为5h剩余（交换={swapped}）", item.FiveHourRemaining, 100);
    Near($"使用量转为周剩余（交换={swapped}）", item.WeeklyRemaining, 88);
    Check($"识别正确周重置（交换={swapped}）", item.WeeklyReset == now.AddDays(5));
}
using (var empty = JsonDocument.Parse("{\"rate_limit\":{\"primary_window\":null}}"))
    Check("缺失额度不伪造满额", CpaParser.Parse(empty.RootElement, "x", "x", "x").Error is not null);
using (var malformed = JsonDocument.Parse("{\"rate_limit\":{\"primary_window\":{\"used_percent\":\"wrong\"}}}"))
    Check("错误类型不伪造满额", CpaParser.Parse(malformed.RootElement, "x", "x", "x").FiveHourRemaining is null);

foreach (var modern in new[] { true, false })
{
    var calls = new List<string>();
    var suffix = modern ? "/v8/management" : "/v0/management";
    using var handler = new FakeHandler(async request =>
    {
        calls.Add(request.RequestUri!.AbsolutePath);
        Check("密钥只在管理请求Authorization头", request.Headers.Authorization?.Parameter == "test-key");
        if (request.Method == HttpMethod.Get)
            return JsonResponse("""{"files":[{"auth_index":"a","name":"codex-demo.json","provider":"codex"},{"auth_index":"b","provider":"codex","disabled":true},{"auth_index":"c","provider":"claude"}]}""");
        var payload = await request.Content!.ReadAsStringAsync();
        Check("上游token占位符而非本地凭据", payload.Contains("$TOKEN$"));
        Check("只代理查询额度", payload.Contains("wham/usage") && payload.Contains("GET"));
        return JsonResponse(JsonSerializer.Serialize(new { status_code = 200, body = Usage() }));
    });
    using var http = new HttpClient(handler);
    var source = new CpaQuotaSource(http, "http://127.0.0.1:8317" + suffix, "test-key");
    var accounts = await source.ReadAsync(default);
    Check($"过滤非Codex及禁用账号（v8={modern}）", accounts.Count == 1);
    Near($"管理代理解析正常（v8={modern}）", accounts[0].WeeklyRemaining, 88);
    Check($"管理路径匹配版本（v8={modern}）",
        calls.SequenceEqual(new[] { suffix + (modern ? "/credentials" : "/auth-files"),
            suffix + (modern ? "/requests/api-call" : "/api-call") }));
}
using (var handler = new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))))
using (var http = new HttpClient(handler))
{
    try
    {
        await new CpaQuotaSource(http, "http://127.0.0.1:8317/v8/management", "test-key").ReadAsync(default);
        Check("鉴权错误不能静默演示", false);
    }
    catch (HttpRequestException error)
    {
        Check("鉴权错误明确暂停自动刷新", CpaQuotaSource.SafeError(error).Contains("暂停"));
        Check("错误不泄露管理密钥", !CpaQuotaSource.SafeError(error).Contains("test-key"));
    }
}
foreach (var badUrl in new[] { "http://example.com/v8/management", "file:///v8/management",
    "https://user:password@example.com/v8/management", "https://example.com/v8/management?key=x" })
{
    try { CpaQuotaSource.ValidateUrl(badUrl); Check("拒绝不安全地址", false); }
    catch (ArgumentException) { Check("拒绝不安全地址", true); }
}
Check("允许本机管理地址", CpaQuotaSource.ValidateUrl("http://127.0.0.1:8317/v8/management").IsLoopback);
Check("允许远端HTTPS", CpaQuotaSource.ValidateUrl("https://example.com/v8/management").Scheme == "https");
passed += await CpaFailureTests.RunAsync();
passed += ViewportTests.Run();
passed += WaterDisplayTests.Run();
passed += TrafficRateTests.Run();
passed += await KeeperTests.RunAsync();
passed += ManagementKeyTests.Run();
passed += await TrafficOrderingTests.RunAsync();
passed += await LocalAuthTests.RunAsync();
passed += await CodexCliTests.RunAsync();
passed += AdditionalWindowTests.Run();
Console.WriteLine($"全部 {passed} 项验证通过。");

static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };

sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        handle(request);
}
