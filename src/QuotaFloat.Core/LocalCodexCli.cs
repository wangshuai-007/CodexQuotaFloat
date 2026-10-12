namespace QuotaFloat.Core;

/// <summary>定位原生Codex CLI；不调用批处理、shell或扫描整个用户目录。</summary>
public static class LocalCodexCli
{
    /// <summary>查找PATH中的codex.exe或Windows Codex应用自带CLI；没有时返回null。</summary>
    public static string? Find()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var path = Path.Combine(directory.Trim().Trim('"'), "codex.exe");
            if (File.Exists(path)) return path;
        }
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (!Directory.Exists(bundled)) return null;
        return new DirectoryInfo(bundled).EnumerateDirectories().OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => Path.Combine(directory.FullName, "codex.exe")).FirstOrDefault(File.Exists);
    }
}
