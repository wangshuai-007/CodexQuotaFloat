using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuotaFloat.Core;

/// <summary>CPA 已配置的 Fast 规则，不代表上游实际执行档位。</summary>
public sealed record CpaFastStatus(bool Managed, bool Global, bool Partial, bool Conflict);

/// <summary>只管理 CPA v8 payload override 列表里属于浮窗的全局 Codex Fast 规则。</summary>
public sealed class CpaFastMode(HttpClient http, string managementUrl, string key)
{
    // 第二个选择器是第一个的子集：用受支持的规则字段标记归属，不扩大匹配范围。
    private const string Rule = """
        {"models":[{"name":"gpt-*","protocol":"codex"},{"name":"gpt-*","protocol":"codex","headers":{"X-CodexQuotaFloat-Rule":"global-fast-v1"}}],"params":{"service_tier":"priority"}}
        """;

    public async Task<CpaFastStatus> ReadAsync(CancellationToken token)
        => Inspect(await ReadPayloadAsync(token));

    public async Task<CpaFastStatus> SetAsync(bool enabled, CancellationToken token)
    {
        var payload = await ReadPayloadAsync(token);
        var status = Inspect(payload);
        if (enabled == status.Managed) return status;
        if (enabled && (status.Global || status.Conflict)) return status;
        var rules = (JsonArray)(RuleList(payload, "override").DeepClone());
        if (enabled) rules.Add(JsonNode.Parse(Rule));
        else
            for (var i = rules.Count - 1; i >= 0; i--)
                if (IsManaged(rules[i])) rules.RemoveAt(i);
        using var request = Request(HttpMethod.Put, "config/requests/payload/override");
        request.Content = JsonContent.Create(rules);
        using var response = await http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var saved = await ReadAsync(token);
        if (saved.Managed != enabled) throw new InvalidOperationException("CPA 未确认 Fast 规则变更");
        return saved;
    }

    private async Task<JsonObject> ReadPayloadAsync(CancellationToken token)
    {
        using var request = Request(HttpMethod.Get, "config/requests/payload");
        using var response = await http.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound &&
            response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var missing = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: token);
            if (StringValue(missing?["error"]) == "not_found") return new();
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: token)) switch
        {
            null => new(),
            JsonObject payload => payload,
            _ => throw new FormatException("CPA payload 配置不是对象")
        };
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var root = CpaQuotaSource.ValidateUrl(managementUrl);
        if (!root.AbsolutePath.EndsWith("/v8/management/", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("CPA Fast 需要 v8 管理地址和管理密钥");
        var request = new HttpRequestMessage(method, new Uri(root, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        return request;
    }

    private static JsonArray RuleList(JsonObject payload, string name) => payload[name] switch
    {
        null => new(),
        JsonArray list => list,
        _ => throw new FormatException("CPA payload 规则不是列表")
    };

    private static string? StringValue(JsonNode? value)
        => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;

    private static bool IsManaged(JsonNode? rule) => JsonNode.DeepEquals(rule, JsonNode.Parse(Rule));

    private static CpaFastStatus Inspect(JsonObject payload)
    {
        var managed = false;
        var global = false;
        var partial = false;
        var conflict = false;
        foreach (var type in new[] { "default", "default-raw", "override", "override-raw", "filter" })
        foreach (var item in RuleList(payload, type))
        {
            if (item is not JsonObject rule || rule["models"] is not JsonArray models)
                throw new FormatException("CPA payload 规则格式不兼容");
            var codex = models.OfType<JsonObject>().Where(model =>
                StringValue(model["protocol"]) is null or "" or "codex");
            if (!codex.Any()) continue;
            if (type == "filter")
            {
                conflict |= rule["params"] is JsonArray paths && paths.Any(path => StringValue(path) is "service_tier");
                continue;
            }
            if (rule["params"] is not JsonObject parameters || !parameters.TryGetPropertyValue("service_tier", out var value)) continue;
            managed |= type == "override" && IsManaged(rule);
            var tier = StringValue(value);
            if (type.EndsWith("-raw", StringComparison.Ordinal) && tier is "\"priority\"" or "\"fast\"")
                tier = tier.Trim('"');
            var overrides = type.StartsWith("override", StringComparison.Ordinal);
            if (tier is "priority" or "fast")
            {
                var wide = overrides && codex.Any(model =>
                    StringValue(model["name"]) is "*" or "gpt-*" &&
                    model.All(field => field.Key is "name" or "protocol" || field.Value is null ||
                        field.Value is JsonArray { Count: 0 } || field.Value is JsonObject { Count: 0 } ||
                        StringValue(field.Value) == ""));
                global |= wide;
                partial |= !wide;
            }
            else if (overrides) conflict = true;
        }
        return new(managed, global && !conflict, partial || global && conflict, conflict);
    }
}
