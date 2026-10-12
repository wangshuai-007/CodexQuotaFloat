using System.Text.Json;
using QuotaFloat.Core;

internal static class LocalAuthLiveCheck
{
    internal static async Task RunAsync()
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        try
        {
            var cli = LocalCodexCli.Find();
            IQuotaSource source = cli is null ? new LocalCodexQuotaSource(http, null) : new CodexCliQuotaSource(cli);
            IReadOnlyList<AccountQuota> accounts;
            try { accounts = await source.ReadAsync(default); }
            finally { (source as IDisposable)?.Dispose(); }
            var valid = accounts.Count == 1 && (accounts[0].FiveHourRemaining is not null && accounts[0].WeeklyRemaining is not null ||
                accounts[0].AdditionalWindows.Any(window => window.Remaining is not null));
            Console.WriteLine(valid ? "本地认证实测：成功；只读官方查询得到有效额度窗口，不输出账号或认证内容。" :
                "本地认证实测：服务响应成功，但未取得有效额度窗口；不可声称额度查询成功。" );
            if (!valid) Environment.ExitCode = 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            HttpRequestException or JsonException or FormatException or OperationCanceledException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine("本地认证实测：" + LocalCodexQuotaSource.SafeError(error));
            Environment.ExitCode = 1;
        }
    }
}
