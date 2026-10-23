using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>只提取session token_count数字；累计增量去重，不重复加缓存输入或推理输出。</summary>
internal sealed class SessionTokenCounter
{
    private long? previous;

    internal (DateTimeOffset Time, long Tokens, string Id)? Read(string line)
    {
        if (!line.Contains("token_count", StringComparison.Ordinal)) return null;
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var kind) ||
            kind.GetString() != "event_msg" || !root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("type", out kind) ||
            kind.GetString() != "token_count") return null;
        if (!payload.TryGetProperty("info", out var info) || info.ValueKind == JsonValueKind.Null) return null;
        if (!root.TryGetProperty("timestamp", out var stamp) || stamp.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(stamp.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var time) ||
            !info.TryGetProperty("total_token_usage", out var total) || !Number(total, out var count))
            throw new FormatException("本地session的Token计量格式不兼容");
        long? last = info.TryGetProperty("last_token_usage", out var lastUsage) && Number(lastUsage, out var value) ? value : null;
        var delta = previous is { } baseline && count >= baseline ? count - baseline : last;
        previous = count;
        return delta is > 0 ? (time, delta.Value, $"{time:O}:{count}:{delta}") : null;
    }

    private static bool Number(JsonElement usage, out long value)
    {
        value = 0;
        return usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("total_tokens", out var field) &&
            field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out value) && value >= 0;
    }
}
