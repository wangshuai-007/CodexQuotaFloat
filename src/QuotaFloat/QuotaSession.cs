using System.Net;
using System.Net.Http;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>刷新互斥与版本隔离；切换源后旧请求无法污染新界面。</summary>
public sealed class QuotaSession : IDisposable
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly HttpClient localHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private CancellationTokenSource requestCancellation = new();
    private int version;
    private AppSettings settings;
    private string key;
    private IQuotaSource? source;
    public IReadOnlyList<AccountQuota> Accounts { get; private set; } = [];
    public string? Error { get; private set; }
    public bool Loading { get; private set; }
    public bool AutoRefreshPaused { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public event Action? Changed;

    public QuotaSession(AppSettings options, string managementKey)
    { settings = options; key = managementKey; }

    /// <summary>清空旧来源数据并取消旧请求，不把CPA失败回退为假数据。</summary>
    public void Configure(AppSettings options, string managementKey)
    {
        version++;
        requestCancellation.Cancel();
        (source as IDisposable)?.Dispose();
        source = null;
        requestCancellation.Dispose();
        requestCancellation = new CancellationTokenSource();
        settings = options;
        key = managementKey;
        Accounts = [];
        UpdatedAt = null;
        Error = null;
        Loading = false;
        AutoRefreshPaused = false;
        Changed?.Invoke();
    }

    /// <summary>读取所选数据源；错误保留旧快照并明确标注，鉴权失败暂停定时刷新。</summary>
    public async Task RefreshAsync()
    {
        if (Loading) return;
        var current = version;
        var token = requestCancellation.Token;
        Loading = true;
        Error = null;
        Changed?.Invoke();
        try
        {
            var active = source ??= settings.UseDemo ? new DemoQuotaSource(settings.DemoCount) :
                settings.UseLocalAuth ? LocalSource() :
                new CpaQuotaSource(http, settings.ManagementUrl, key);
            var items = await active.ReadAsync(token);
            if (current != version) return;
            Accounts = items;
            UpdatedAt = DateTimeOffset.Now;
            AutoRefreshPaused = false;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException or
            FormatException or ArgumentException or OperationCanceledException or TimeoutException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {
            if (current != version) return;
            Error = settings.UseLocalAuth && !settings.UseDemo ? LocalCodexQuotaSource.SafeError(error) : CpaQuotaSource.SafeError(error);
            AutoRefreshPaused = error is ArgumentException || settings.UseLocalAuth && error is FormatException ||
                error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };
        }
        finally
        {
            if (current == version) { Loading = false; Changed?.Invoke(); }
        }
    }

    /// <summary>结束请求与HTTP资源生命周期。</summary>
    public void Dispose()
    {
        version++;
        requestCancellation.Cancel();
        requestCancellation.Dispose();
        http.Dispose();
        localHttp.Dispose();
        (source as IDisposable)?.Dispose();
    }

    private IQuotaSource LocalSource()
    {
        var cli = string.IsNullOrWhiteSpace(settings.LocalAuthPath) ? LocalCodexCli.Find() : null;
        return cli is null ? new LocalCodexQuotaSource(localHttp, settings.LocalAuthPath) : new CodexCliQuotaSource(cli);
    }

    /// <summary>手动刷新重新连接本地CLI，及时读取其它程序更新的登录状态。</summary>
    public void PrepareManualRefresh()
    {
        if (Loading || !settings.UseLocalAuth || settings.UseDemo) return;
        (source as IDisposable)?.Dispose();
        source = null;
    }
}
