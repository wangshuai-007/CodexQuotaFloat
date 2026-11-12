using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

internal static class UpdateTests
{
    internal static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(string name, bool valid)
        {
            if (!valid) throw new InvalidOperationException(name);
            Console.WriteLine("通过：" + name); passed++;
        }
        var content = Encoding.UTF8.GetBytes("installer fixture");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        string Metadata(string tag = "v1.2.0", bool prerelease = false, bool draft = false, string? url = null, string? digest = null) =>
            JsonSerializer.Serialize(new { tag_name = tag, prerelease, draft, assets = new[] {
                new { name = "CodexQuotaFloat-1.2.0-win-x64-setup.exe",
                    browser_download_url = url ?? GitHubUpdates.Repository + "/releases/download/v1.2.0/CodexQuotaFloat-1.2.0-win-x64-setup.exe",
                    digest = digest ?? "sha256:" + hash } } });
        UpdateRelease? Parse(string json, string current = "1.1.0")
        {
            using var data = JsonDocument.Parse(json);
            return GitHubUpdates.Parse(data.RootElement, Version.Parse(current));
        }
        var release = Parse(Metadata())!;
        Check("识别正式新版安装包", release.Version == new Version(1, 2, 0));
        Check("相同版本不升级", Parse(Metadata(), "1.2.0") is null);
        Check("不降级", Parse(Metadata(), "2.0.0") is null);
        Check("忽略预览版", Parse(Metadata(prerelease: true)) is null);
        Check("忽略草稿", Parse(Metadata(draft: true)) is null);
        foreach (var invalid in new[] { Metadata(url: "https://evil.example/setup.exe"), Metadata(digest: "bad"),
            Metadata(tag: "v1.2.0-beta"), Metadata(tag: "v9.0.0") })
        {
            try { Parse(invalid); Check("拒绝不兼容发布信息", false); }
            catch (FormatException) { Check("拒绝不兼容发布信息", true); }
        }
        using var client = new GitHubUpdates(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }));
        var path = await client.DownloadAsync(release, null, default);
        Check("合法安装包SHA256通过", File.ReadAllBytes(path).SequenceEqual(content));
        File.Delete(path);
        try { await client.DownloadAsync(release with { Digest = new string('0', 64) }, null, default); Check("拒绝哈希不匹配", false); }
        catch (FormatException) { Check("拒绝哈希不匹配", true); }
        using var redirect = new GitHubUpdates(new Handler(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://evil.example/payload");
            return response;
        }));
        try { await redirect.DownloadAsync(release, null, default); Check("阻断非GitHub重定向", false); }
        catch (FormatException) { Check("阻断非GitHub重定向", true); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await client.DownloadAsync(release, null, cancelled.Token); Check("支持取消下载", false); }
        catch (OperationCanceledException) { Check("支持取消下载", true); }
        return passed;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(action(request)); }
    }
}
