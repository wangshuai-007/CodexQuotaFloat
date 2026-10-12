using System.Text;
using System.Text.Json;
using QuotaFloat.Core;

internal static class LocalAuthEncodingTests
{
    internal static async Task RunAsync(Action<string, bool> check)
    {
        var path = Path.Combine(Path.GetTempPath(), "QuotaFloatEncoding-" + Guid.NewGuid() + ".json");
        var json = JsonSerializer.Serialize(new { auth_mode = "chatgpt", tokens = new {
            access_token = "fixture-encoding-token", account_id = "fixture-account" } });
        try
        {
            foreach (var encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true),
                new UnicodeEncoding(false, true), new UnicodeEncoding(true, true) })
            {
                var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(json)).ToArray();
                await File.WriteAllBytesAsync(path, bytes);
                check("本地认证兼容UTF8及带BOM的UTF16", await LocalCodexAuth.ReadAsync(path, default) is not null);
                check("编码兼容读取不改写认证", (await File.ReadAllBytesAsync(path)).SequenceEqual(bytes));
            }
            foreach (var invalid in new byte[][] { Encoding.UTF8.GetBytes("private-token malformed-json"), [0xff, 0xfe, 0x7b], [0xff, 0x80] })
            {
                await File.WriteAllBytesAsync(path, invalid);
                try { await LocalCodexAuth.ReadAsync(path, default); check("拒绝无效认证编码", false); }
                catch (FormatException error)
                { check("认证解析错误与额度接口错误区分", error.Message.StartsWith("认证文件") && !error.Message.Contains("private-token")); }
            }
        }
        finally { File.Delete(path); }
    }
}
