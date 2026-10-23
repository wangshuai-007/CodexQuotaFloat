using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>解析 CPA 代理返回的 Codex 双额度窗口，不将缺失字段视为100%。</summary>
public static class CpaParser
{
    /// <summary>将上游 usage JSON 映射为账号额度；按窗口长度识别5h/周，兼容缺少长度的旧格式。</summary>
    public static AccountQuota Parse(JsonElement usage, string id, string name, string plan)
    {
        if (usage.ValueKind != JsonValueKind.Object)
            throw new FormatException("CPA 额度响应不是 JSON 对象。");
        var windows = new List<(JsonElement Value, string Key)>();
        if (usage.TryGetProperty("rate_limit", out var limit) && limit.ValueKind == JsonValueKind.Object)
            foreach (var key in new[] { "primary_window", "secondary_window" })
                if (limit.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object)
                    windows.Add((value, key));

        var five = Find(windows, 18000, "primary_window");
        var week = Find(windows, 604800, "secondary_window");
        var fiveRemaining = Remaining(five);
        var weekRemaining = Remaining(week);
        var additional = windows.Where(item => Number(item.Value, "limit_window_seconds") is { } seconds &&
            seconds > 0 && seconds != 18000 && seconds != 604800)
            .Select(item => new AdditionalQuotaWindow(Number(item.Value, "limit_window_seconds")!.Value,
                Remaining(item.Value), Reset(item.Value))).ToArray();
        return new AccountQuota(id, name, Text(usage, "plan_type") ?? plan,
            fiveRemaining, weekRemaining, Reset(five), Reset(week),
            additional.Any(window => window.IsMonthly && window.Remaining is not null) ? null :
            fiveRemaining is null || weekRemaining is null ? additional.Length > 0 ?
                "服务返回其他周期额度 · 不计入5h/7天水位" : "额度窗口缺失 · 不计入水位" : null)
            { AdditionalWindows = additional };
    }

    private static JsonElement? Find(List<(JsonElement Value, string Key)> windows, int seconds, string fallback)
    {
        foreach (var (value, _) in windows)
            if (Number(value, "limit_window_seconds") == seconds) return value;
        foreach (var (value, key) in windows)
            if (key == fallback && Number(value, "limit_window_seconds") is null) return value;
        return null;
    }

    private static double? Remaining(JsonElement? window)
    {
        if (window is not { } value || Number(value, "used_percent") is not { } used) return null;
        return Math.Clamp(100 - used, 0, 100);
    }

    private static DateTimeOffset? Reset(JsonElement? window)
    {
        if (window is not { } value || !value.TryGetProperty("reset_at", out var reset)) return null;
        if (reset.TryGetInt64Safe(out var unix))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return reset.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(reset.GetString(), out var date) ? date : null;
    }

    /// <summary>安全读取可选字符串字段。</summary>
    public static string? Text(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static double? Number(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number &&
        field.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static bool TryGetInt64Safe(this JsonElement value, out long number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out number) ||
            value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number);
    }
}
