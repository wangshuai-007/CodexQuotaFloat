using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>受限stdio客户端，用于账号、额度及配置/钩子状态读取，不创建对话或推理。</summary>
internal sealed class CodexRpc : IDisposable
{
    private readonly Process process;
    private readonly CancellationTokenSource stopStderr = new();
    private readonly Task stderr;
    private int id;
    private bool disposed;

    internal CodexRpc(string executable)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false) };
        start.ArgumentList.Add("app-server");
        process = Process.Start(start) ?? throw new IOException("无法启动Codex CLI");
        stderr = DrainAsync(process.StandardError, stopStderr.Token);
    }

    internal async Task InitializeAsync(CancellationToken token, bool experimental = false)
    {
        using var response = await RequestAsync("initialize", new { clientInfo = new
            { name = "codex_quota_float", title = "Codex额度浮窗", version = "0.2" },
            capabilities = new { experimentalApi = experimental } }, token);
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
            using var message = ParseMessage(await ReadLineAsync(token));
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

    private static JsonDocument ParseMessage(string line)
    {
        try { return JsonDocument.Parse(line.TrimStart('\ufeff')); }
        catch (JsonException)
        { throw new FormatException("Codex CLI未返回有效JSON协议；请更新原生codex.exe，或手动选择ChatGPT认证文件"); }
    }

    private async Task<string> ReadLineAsync(CancellationToken token)
    {
        var text = new System.Text.StringBuilder();
        var buffer = new char[1];
        while (text.Length < 2 * 1024 * 1024)
        {
            int read;
            try { read = await process.StandardOutput.ReadAsync(buffer.AsMemory(), token); }
            catch (DecoderFallbackException)
            { throw new FormatException("Codex CLI协议不是有效UTF8；请更新原生codex.exe，不显示协议正文"); }
            if (read == 0) throw new IOException("Codex CLI已退出");
            if (buffer[0] == '\n') return text.ToString();
            if (buffer[0] != '\r') text.Append(buffer[0]);
        }
        throw new FormatException("Codex CLI响应过大");
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false) > 0) { /* 不保留诊断正文。 */ }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        _ = Task.Run(CloseProcessAsync);
    }

    private async Task CloseProcessAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                if (!process.HasExited) process.Kill();
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (process.HasExited) { /* 已自行退出。 */ }
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException)
        { Trace.TraceWarning("Codex CLI 清理未完成：{0}", error.GetType().Name); }
        finally
        {
            stopStderr.Cancel();
            try
            {
                process.StandardOutput.Dispose();
                process.StandardError.Dispose();
                process.StandardInput.Dispose();
                await stderr.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopStderr.IsCancellationRequested) { /* 退出时主动取消。 */ }
            catch (Exception error) when (error is IOException or InvalidOperationException or TimeoutException)
            { Trace.TraceWarning("Codex CLI 管道关闭：{0}", error.GetType().Name); }
            finally { process.Dispose(); stopStderr.Dispose(); }
        }
    }
}
