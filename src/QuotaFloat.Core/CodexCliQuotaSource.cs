using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>通过官方app-server复用CLI认证，本工具不直接提取系统令牌或创建推理请求。</summary>
public sealed class CodexCliQuotaSource(string executable) : IQuotaSource, IDisposable
{
    private CodexRpc? rpc;
    private bool disposed;
    private DateTimeOffset connectedAt;
    /// <summary>只调用account/read和account/rateLimits/read，Codex自行管理登录缓存。</summary>
    public async Task<IReadOnlyList<AccountQuota>> ReadAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var stage = "启动CLI";
        try
        {
            if (rpc is not null && DateTimeOffset.UtcNow - connectedAt > TimeSpan.FromMinutes(1)) ResetConnection();
            if (rpc is null)
            {
                rpc = new CodexRpc(executable);
                await rpc.InitializeAsync(timeout.Token);
                connectedAt = DateTimeOffset.UtcNow;
            }
            stage = "读取账号";
            return await ReadAccountAsync(rpc, timeout.Token, value => stage = value);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { ResetConnection(); throw new TimeoutException($"本地查询超时（{stage}，30秒）；请检查Codex网络/代理后刷新"); }
        catch { ResetConnection(); throw; }
    }

    private static async Task<IReadOnlyList<AccountQuota>> ReadAccountAsync(CodexRpc connection, CancellationToken token, Action<string> stage)
    {
        using var identity = await connection.RequestAsync("account/read", new { refreshToken = false }, token);
        var root = identity.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("account", out var account) ||
            account.ValueKind != JsonValueKind.Object || CpaParser.Text(account, "type") != "chatgpt")
            throw new FormatException("Codex CLI未使用ChatGPT认证；可能与桌面登录不同。请选ChatGPT的auth.json，API Key无法查订阅额度");
        var name = CpaParser.Text(account, "email") ?? "本地Codex账号";
        var plan = CpaParser.Text(account, "planType") ?? "Codex";
        stage("读取额度");
        using var limits = await connection.RequestAsync("account/rateLimits/read", new { }, token);
        return [Parse(limits.RootElement, name, plan)];
    }

    /// <summary>切换来源或退出时只关闭本工具启动的CLI进程。</summary>
    public void Dispose() { disposed = true; ResetConnection(); }

    private void ResetConnection()
    {
        var previous = rpc;
        rpc = null;
        previous?.Dispose();
    }

    /// <summary>选择codex计量桶并按窗口分钟数转换为现有额度模型；不伪造缺失窗口。</summary>
    public static AccountQuota Parse(JsonElement result, string name, string plan)
    {
        var limit = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("rateLimitsByLimitId", out var buckets) &&
            buckets.ValueKind == JsonValueKind.Object && buckets.TryGetProperty("codex", out var codex) ? codex :
            result.ValueKind == JsonValueKind.Object && result.TryGetProperty("rateLimits", out var single) ? single : default;
        var usage = new { plan_type = CpaParser.Text(limit, "planType") ?? plan, rate_limit = new
            { primary_window = Window(limit, "primary"), secondary_window = Window(limit, "secondary") } };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(usage));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name)));
        return CpaParser.Parse(json.RootElement, "local-cli:" + hash, name, plan);
    }

    private static object? Window(JsonElement limit, string key)
    {
        if (limit.ValueKind != JsonValueKind.Object || !limit.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Object) return null;
        double? Number(string field) => value.TryGetProperty(field, out var number) && number.ValueKind == JsonValueKind.Number &&
            number.TryGetDouble(out var parsed) && double.IsFinite(parsed) ? parsed : null;
        return new { used_percent = Number("usedPercent"), limit_window_seconds = Number("windowDurationMins") * 60,
            reset_at = Number("resetsAt") };
    }
}
