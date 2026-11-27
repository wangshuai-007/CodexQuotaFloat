using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using QuotaFloat.Core;

internal static class CpaFastTests
{
    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(string name, bool pass)
        {
            if (!pass) throw new InvalidOperationException(name);
            Console.WriteLine("通过：" + name);
            passed++;
        }
        const string path = "/proxy/v8/management/config/requests/payload";
        var payload = JsonNode.Parse("""
            {"override":[{"models":[{"name":"gpt-*","protocol":"codex"}],"params":{"reasoning.effort":"high"}}]}
            """)!.AsObject();
        var original = payload.DeepClone();
        var missing = false;
        var keepWrites = true;
        var writes = 0;
        using var http = new HttpClient(new FakeHandler(async request =>
        {
            if (request.Headers.Authorization?.ToString() != "Bearer fixture") throw new InvalidOperationException("管理鉴权");
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == path)
                return missing ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    { Content = new StringContent("""{"error":"not_found"}""", Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json") };
            if (request.Method != HttpMethod.Put || request.RequestUri!.AbsolutePath != path + "/override")
                throw new InvalidOperationException("写入范围错误");
            var rules = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsArray();
            if (keepWrites) { payload["override"] = rules; missing = false; }
            writes++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var fast = new CpaFastMode(http, "https://example.invalid/proxy/v8/management", "fixture");
        Check("初次读取 Fast 状态不写入 CPA", !(await fast.ReadAsync(default)).Global && writes == 0);
        Check("开启 CPA Fast 回读为全局且保留已有规则", await fast.SetAsync(true, default) is { Managed: true, Global: true } &&
            payload["override"]!.AsArray().Count == 2 && JsonNode.DeepEquals(payload["override"]![0], original["override"]![0]));
        Check("重复开启不覆盖规则表", (await fast.SetAsync(true, default)).Managed && writes == 1);
        Check("关闭只删除浮窗规则", !(await fast.SetAsync(false, default)).Global && JsonNode.DeepEquals(payload, original));
        payload = JsonNode.Parse("""
            {"override":[{"models":[{"name":"gpt-*","protocol":"codex"}],"params":{"service_tier":"priority"}}]}
            """)!.AsObject();
        Check("外部全局 Fast 可识别，不会被浮窗接管或删除", await fast.SetAsync(true, default) is { Managed: false, Global: true } &&
            (await fast.SetAsync(false, default)).Global && writes == 2);
        payload = JsonNode.Parse("""
            {"override-raw":[{"models":[{"name":"my-fast","protocol":"codex"}],"params":{"service_tier":"\"priority\""}}]}
            """)!.AsObject();
        Check("外部局部 Fast 与全局状态区分", await fast.ReadAsync(default) is { Global: false, Partial: true });
        payload["filter"] = JsonNode.Parse("""[{"models":[{"name":"gpt-*","protocol":"codex"}],"params":["service_tier"]}]""");
        Check("有档位过滤冲突则不写", (await fast.SetAsync(true, default)).Conflict && writes == 2);
        missing = true;
        Check("CPA 缺少 payload 节点可初始化 Fast", !(await fast.ReadAsync(default)).Global);
        keepWrites = false;
        var unconfirmed = false;
        try { await fast.SetAsync(true, default); }
        catch (InvalidOperationException error) when (error.Message.Contains("未确认")) { unconfirmed = true; }
        Check("写入未回读确认不显示成功", unconfirmed && writes == 3);
        return passed;
    }
}
