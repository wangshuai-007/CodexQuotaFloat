using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>仅保留时间/Token/请求ID的60秒窗口，不保留API密钥、邮箱、模型输入或响应。</summary>
public sealed class TrafficAccumulator
{
    private readonly List<(DateTimeOffset Time, long Tokens, string? Id)> events = [];
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
    private DateTimeOffset? sampledAt;
    private long addedTokens;
    private int addedRequests;
    private double? tokensPerSecond, requestsPerSecond, sampleSeconds;

    /// <summary>读取只读记录快照或旧版usage中的details；格式错误不部分写入。</summary>
    public void Ingest(JsonElement root, DateTimeOffset now)
    {
        Prune(now);
        var previous = events.ToArray();
        occurrences.Clear();
        addedTokens = 0; addedRequests = 0;
        try { ReadSnapshot(root, now); UpdateRate(now); }
        catch
        {
            events.Clear(); events.AddRange(previous);
            Prune(now);
            throw;
        }
    }

    private void ReadSnapshot(JsonElement root, DateTimeOffset now)
    {
        if (root.ValueKind == JsonValueKind.Array) ReadRecords(root, now);
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
            ReadRecords(records, now);
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            var found = WalkSnapshot(usage, now);
            if (!found && !(usage.TryGetProperty("apis", out var apis) && apis.ValueKind == JsonValueKind.Object && !apis.EnumerateObject().Any()))
                throw new FormatException("流量快照缺少请求明细，无法计算最近60秒");
        }
        else throw new FormatException("流量统计格式不兼容");
    }

    /// <summary>按最近60秒统计总Token和请求数，输出12个5秒桶；无记录是明确的零。</summary>
    public TrafficSnapshot Snapshot(DateTimeOffset now)
    {
        Prune(now);
        var buckets = new long[12];
        foreach (var item in events)
        {
            var index = Math.Clamp(11 - (int)((now - item.Time).TotalSeconds / 5), 0, 11);
            buckets[index] = SaturatingAdd(buckets[index], item.Tokens);
        }
        return new TrafficSnapshot(events.Aggregate(0L, (sum, item) => SaturatingAdd(sum, item.Tokens)),
            events.Count, buckets, sampledAt)
        { TokensPerSecond = tokensPerSecond, RequestsPerSecond = requestsPerSecond, SampleSeconds = sampleSeconds }.ForDisplay(now);
    }

    /// <summary>切换CPA或数据源时清除旧窗口。</summary>
    public void Clear()
    {
        events.Clear(); seen.Clear(); sampledAt = null;
        tokensPerSecond = null; requestsPerSecond = null; sampleSeconds = null;
    }

    private void UpdateRate(DateTimeOffset now)
    {
        sampleSeconds = sampledAt is { } previous ? (now - previous).TotalSeconds : null;
        var valid = sampleSeconds is > 0;
        tokensPerSecond = valid ? addedTokens / sampleSeconds : null;
        requestsPerSecond = valid ? addedRequests / sampleSeconds : null;
        sampledAt = now;
    }

    private void ReadRecords(JsonElement records, DateTimeOffset now)
    {
        foreach (var record in records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object ||
                !record.TryGetProperty("timestamp", out var stamp) ||
                stamp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(stamp.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var time))
                throw new FormatException("流量记录缺少有效时间");
            if (time <= now.AddSeconds(-60) || time > now.AddSeconds(5)) continue;
            if (!record.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object ||
                !tokens.TryGetProperty("total_tokens", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var total) || total < 0)
                throw new FormatException("流量记录缺少有效Token");
            var id = record.TryGetProperty("request_id", out var requestId) && requestId.ValueKind == JsonValueKind.String
                ? requestId.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(record.GetRawText())));
                var occurrence = occurrences.GetValueOrDefault(hash) + 1;
                occurrences[hash] = occurrence;
                id = hash + ":" + occurrence;
            }
            // 无request_id的旧快照仅保留不可逆摘要，避免留存API密钥等响应字段。
            if (id is not null && !seen.Add(id)) continue;
            events.Add((time, total, id));
            addedTokens = SaturatingAdd(addedTokens, total);
            addedRequests++;
            if (events.Count > 20_000) throw new FormatException("流量窗口记录过多");
        }
        if (events.Count > 20_000) throw new FormatException("流量窗口记录过多");
    }

    private bool WalkSnapshot(JsonElement node, DateTimeOffset now, int depth = 0)
    {
        if (depth > 8) throw new FormatException("流量快照层级过深");
        if (node.ValueKind != JsonValueKind.Object) return false;
        var found = false;
        foreach (var field in node.EnumerateObject())
        {
            if (field.Name == "details")
            {
                if (field.Value.ValueKind != JsonValueKind.Array) throw new FormatException("流量明细不是数组");
                ReadRecords(field.Value, now); found = true;
            }
            else if (field.Value.ValueKind == JsonValueKind.Object) found |= WalkSnapshot(field.Value, now, depth + 1);
        }
        return found;
    }

    private void Prune(DateTimeOffset now)
    {
        events.RemoveAll(item => item.Time <= now.AddSeconds(-60));
        seen.Clear();
        foreach (var item in events) if (item.Id is not null) seen.Add(item.Id);
    }

    private static long SaturatingAdd(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
}
