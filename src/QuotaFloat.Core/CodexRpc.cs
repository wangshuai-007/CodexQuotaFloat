using System.Diagnostics;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>受限stdio客户端，只调用初始化、账号读取和额度读取，不创建对话或推理。</summary>
internal sealed class CodexRpc : IDisposable
{
    private readonly Process process;
    private readonly Task stderr;
    private int id;
    private bool disposed;

    internal CodexRpc(string executable)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("app-server");
        process = Process.Start(start) ?? throw new IOException("无法启动Codex CLI");
        stderr = DrainAsync(process.StandardError);
    }

    internal async Task InitializeAsync(CancellationToken token)
    {
        using var response = await RequestAsync("initialize", new { clientInfo = new
            { name = "codex_quota_float", title = "Codex额度浮窗", version = "0.2" } }, token);
        await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}".AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
    }

    internal async Task<JsonDocument> RequestAsync(string method, object parameters, CancellationToken token)
    {
        var requestId = ++id;
        var payload = JsonSerializer.Serialize(new { id = requestId, method, @params = parameters });
        await process.StandardInput.WriteLineAsync(payload.AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
        for (var i = 0; i < 100; i++)
        {
            using var message = JsonDocument.Parse(await ReadLineAsync(token));
            var root = message.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var actual) ||
                actual.ValueKind != JsonValueKind.Number || !actual.TryGetInt32(out var value) || value != requestId) continue;
            if (root.TryGetProperty("error", out _))
                throw new FormatException("Codex CLI无法读取账号额度；请确认ChatGPT登录及CLI版本，不显示协议错误正文");
            if (!root.TryGetProperty("result", out var result)) throw new FormatException("Codex CLI响应缺少结果");
            return JsonDocument.Parse(result.GetRawText());
        }
        throw new FormatException("Codex CLI响应过多，无法匹配额度请求");
    }

    private async Task<string> ReadLineAsync(CancellationToken token)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[1];
        while (text.Length < 2 * 1024 * 1024)
        {
            if (await process.StandardOutput.ReadAsync(buffer.AsMemory(), token) == 0) throw new IOException("Codex CLI已退出");
            if (buffer[0] == '\n') return text.ToString();
            if (buffer[0] != '\r') text.Append(buffer[0]);
        }
        throw new FormatException("Codex CLI响应过大");
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0) { /* 不保留含个人信息的诊断输出。 */ }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(200))
            {
                try { process.Kill(); }
                catch (InvalidOperationException) when (process.HasExited) { process.WaitForExit(); }
                process.WaitForExit();
            }
            stderr.GetAwaiter().GetResult();
        }
        finally { process.Dispose(); }
    }
}
