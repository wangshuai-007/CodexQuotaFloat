using System.Text;

namespace QuotaFloat.Core;

/// <summary>后台增量只读本机session日志；只保留时间和Token数，不持久化会话正文。</summary>
public sealed class LocalSessionTrafficSource
{
    private const int ReadLimit = 4 * 1024 * 1024;
    private readonly string directory;
    private readonly Dictionary<string, Cursor> cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(DateTimeOffset Time, long Tokens, string Id)> events = [];
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private DateTimeOffset? discoveredAt;
    private string[] discovered = [];

    /// <summary>读取指定sessions目录；留空时遵循CODEX_HOME，复用认证路径的本机目录校验。</summary>
    public LocalSessionTrafficSource(string? sessionsDirectory = null)
    {
        directory = sessionsDirectory ?? Path.Combine(Path.GetDirectoryName(LocalCodexAuth.ResolvePath(null))!, "sessions");
    }

    /// <summary>每秒后台读取新增记录，统计本机所有活跃session，不按账号推断或连接网络。</summary>
    public Task<TrafficSnapshot> ReadAsync(DateTimeOffset now, CancellationToken token = default) =>
        Task.Run(() => Read(now, token), token);

    private TrafficSnapshot Read(DateTimeOffset now, CancellationToken token)
    {
        if (!Directory.Exists(directory)) return Unknown("未找到本地Codex sessions目录");
        var files = RecentFiles(now, token);
        if (files.Length > 512) throw new FormatException("本地活跃session超过512个，无法完整统计");
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            if (!cursors.TryGetValue(file.FullName, out var cursor) || file.Length < cursor.Offset || file.CreationTimeUtc != cursor.Created)
                cursors[file.FullName] = cursor = new Cursor { Created = file.CreationTimeUtc };
            ReadFile(file, cursor, now, token);
        }
        var active = files.Select(file => file.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in cursors.Keys.Where(path => !active.Contains(path)).ToArray()) cursors.Remove(path);
        if (cursors.Values.Any(cursor => cursor.ReadyAt > now)) return Unknown("正在建立本地session的60秒完整统计窗口");
        events.RemoveAll(item => item.Time <= now.AddSeconds(-60));
        if (events.Count > 20000) throw new FormatException("本地60秒计量记录超过上限");
        seen.Clear();
        foreach (var item in events) seen.Add(item.Id);
        var buckets = new long[12];
        foreach (var item in events)
        {
            var index = Math.Clamp(11 - (int)((now - item.Time).TotalSeconds / 5), 0, 11);
            buckets[index] = Add(buckets[index], item.Tokens);
        }
        return new(events.Aggregate(0L, (sum, item) => Add(sum, item.Tokens)), null, buckets, now)
            { SourceLabel = "本地session · 本机全部会话", IsLocalSession = true };
    }

    private FileInfo[] RecentFiles(DateTimeOffset now, CancellationToken token)
    {
        if (discoveredAt is null || now - discoveredAt >= TimeSpan.FromSeconds(5))
        {
            discovered = Directory.EnumerateFiles(directory, "rollout-*.jsonl", new EnumerationOptions
                { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint })
                .Where(path => { token.ThrowIfCancellationRequested(); return File.GetLastWriteTimeUtc(path) >= now.UtcDateTime.AddSeconds(-65); })
                .Take(513).ToArray();
            discoveredAt = now;
        }
        return discovered.Select(path => new FileInfo(path)).Where(file => file.Exists &&
            file.LastWriteTimeUtc >= now.UtcDateTime.AddSeconds(-65)).ToArray();
    }

    private void ReadFile(FileInfo file, Cursor cursor, DateTimeOffset now, CancellationToken token)
    {
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
        var skip = cursor.Offset == 0 && stream.Length > ReadLimit;
        if (skip) cursor.ReadyAt = now.AddSeconds(60);
        stream.Position = skip ? stream.Length - ReadLimit : cursor.Offset;
        var consumed = stream.Position;
        using var line = new MemoryStream();
        var buffer = new byte[65536];
        var budget = ReadLimit;
        while (budget > 0)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, Math.Min(buffer.Length, budget));
            if (read == 0) break;
            budget -= read;
            for (var i = 0; i < read; i++)
            {
                consumed++;
                if (buffer[i] != (byte)'\n')
                {
                    if (!skip) line.WriteByte(buffer[i]);
                    if (line.Length > 2 * 1024 * 1024) throw new FormatException("本地session单条记录超过2MiB，无法完整统计");
                    continue;
                }
                if (!skip && line.Length > 0) Ingest(cursor, Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length), now);
                line.SetLength(0);
                skip = false;
                cursor.Offset = consumed;
            }
        }
        if (budget == 0 && stream.Position < stream.Length) throw new FormatException("本地session新增数据超过4MiB，等待后续读取");
    }

    private void Ingest(Cursor cursor, string line, DateTimeOffset now)
    {
        var record = cursor.Counter.Read(line);
        if (record is not { } item || item.Time <= now.AddSeconds(-60) || item.Time > now.AddSeconds(5) || !seen.Add(item.Id)) return;
        events.Add(item);
        if (events.Count > 20000) throw new FormatException("本地60秒计量记录超过上限");
    }

    private static long Add(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
    private static TrafficSnapshot Unknown(string message) => TrafficSnapshot.Unknown(message) with
        { SourceLabel = "本地session · 本机全部会话", IsLocalSession = true };

    private sealed class Cursor
    {
        internal long Offset;
        internal DateTime Created;
        internal DateTimeOffset? ReadyAt;
        internal readonly SessionTokenCounter Counter = new();
    }
}
