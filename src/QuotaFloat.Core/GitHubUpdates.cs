using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>仅使用固定仓库正式Release与固定命名的Windows x64安装包。</summary>
public sealed record UpdateRelease(Version Version, string Tag, Uri Installer, string FileName, string Digest);

/// <summary>只读检查GitHub正式版，限定下载主机并核对GitHub提供的SHA256。</summary>
public sealed class GitHubUpdates : IDisposable
{
    public const string Repository = "https://github.com/wangshuai-007/CodexQuotaFloat";
    private readonly HttpClient client;
    public GitHubUpdates(HttpMessageHandler? handler = null)
    {
        client = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexQuotaFloat-Updater/1.0");
    }

    /// <summary>仅比较三段正式版本，不将预览版或草稿作为升级。</summary>
    public static UpdateRelease? Parse(JsonElement root, Version current)
    {
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var plain = tag.StartsWith('v') ? tag[1..] : tag;
        if (!System.Text.RegularExpressions.Regex.IsMatch(plain, @"^\d+\.\d+\.\d+$") ||
            !Version.TryParse(plain, out var version)) throw new FormatException("Release版本格式不兼容");
        if (version <= current) return null;
        var name = $"CodexQuotaFloat-{plain}-win-x64-setup.exe";
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) throw new FormatException("新版尚未提供兼容的安装包");
        var asset = assets[0];
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        var expected = Repository + "/releases/download/" + tag + "/" + name;
        if (url.AbsoluteUri != expected) throw new FormatException("安装包地址不属于本项目Release");
        var digest = asset.TryGetProperty("digest", out var field) ? field.GetString() ?? "" : "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$"))
            throw new FormatException("GitHub未提供安装包SHA256，已拒绝自动更新");
        return new(version, tag, url, name, digest[7..]);
    }

    /// <summary>查询固定公开仓库最新正式发布；不需要令牌，不上传本地配置。</summary>
    public async Task<UpdateRelease?> CheckAsync(Version current, CancellationToken token)
    {
        using var response = await client.GetAsync("https://api.github.com/repos/wangshuai-007/CodexQuotaFloat/releases/latest",
            HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        await CopyLimitedAsync(stream, buffer, 1024 * 1024, null, token);
        using var json = JsonDocument.Parse(buffer.ToArray());
        return Parse(json.RootElement, current);
    }

    /// <summary>下载到本用户私有随机目录，核验完成才返回安装包；失败清除本次临时文件。</summary>
    public async Task<string> DownloadAsync(UpdateRelease release, IProgress<int>? progress, CancellationToken token)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexQuotaFloat", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, release.FileName);
        try
        {
            using var response = await DownloadResponseAsync(release.Installer, token);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await CopyLimitedAsync(input, output, 512L * 1024 * 1024, progress, token, response.Content.Headers.ContentLength);
            await using var file = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!hash.Equals(release.Digest, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("安装包SHA256校验失败，禁止运行");
            return path;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private async Task<HttpResponseMessage> DownloadResponseAsync(Uri uri, CancellationToken token)
    {
        for (var i = 0; i < 5; i++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                throw new FormatException("拒绝不受信任的下载地址");
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new FormatException("下载重定向缺少地址");
            uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
        }
        throw new FormatException("下载重定向次数过多");
    }

    private static async Task CopyLimitedAsync(Stream input, Stream output, long limit, IProgress<int>? progress,
        CancellationToken token, long? length = null)
    {
        if (length > limit) throw new FormatException("下载文件超过安全大小上限");
        var bytes = new byte[65536];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(bytes, token)) != 0)
        {
            total += count;
            if (total > limit) throw new FormatException("下载文件超过安全大小上限");
            await output.WriteAsync(bytes.AsMemory(0, count), token);
            if (length is > 0) progress?.Report((int)Math.Min(100, total * 100 / length.Value));
        }
    }

    public void Dispose() => client.Dispose();
}
