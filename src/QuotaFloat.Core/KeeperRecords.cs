using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuotaFloat.Core;

internal sealed record KeeperTokens([property: JsonPropertyName("total_tokens")] long Total);
internal sealed record KeeperRecord(
    [property: JsonPropertyName("request_id")] string Id,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Time,
    [property: JsonPropertyName("tokens")] KeeperTokens Tokens);

internal static class KeeperRecords
{
    internal static string? ReadPage(JsonElement root, DateTimeOffset now, List<KeeperRecord> records)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("events", out var events) ||
            events.ValueKind != JsonValueKind.Array || !root.TryGetProperty("has_more", out var more) ||
            more.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new FormatException("Keeper请求明细格式不兼容");
        var allOld = events.GetArrayLength() > 0;
        foreach (var item in events.EnumerateArray())
        {
            if (!item.TryGetProperty("timestamp", out var stamp) || stamp.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var time)) throw new FormatException("Keeper记录缺少有效时间");
            if (time <= now.AddSeconds(-60)) continue;
            allOld = false;
            if (time > now.AddSeconds(5)) throw new FormatException("Keeper时间与本机相差过大，请校准时间");
            if (!item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) ||
                !item.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object ||
                !tokens.TryGetProperty("total_tokens", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var total) || total < 0)
                throw new FormatException("Keeper记录缺少稳定ID或有效Token");
            records.Add(new(id.GetString()!, time, new(total)));
        }
        if (allOld || !more.GetBoolean()) return null;
        if (events.GetArrayLength() == 0 || !root.TryGetProperty("next_cursor", out var cursor) ||
            cursor.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(cursor.GetString()))
            throw new FormatException("Keeper分页不完整，请更新Keeper或检查接口版本");
        return cursor.GetString();
    }
}
