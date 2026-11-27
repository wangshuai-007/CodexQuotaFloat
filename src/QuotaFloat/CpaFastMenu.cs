using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using QuotaFloat.Core;

namespace QuotaFloat;

public partial class MainWindow
{
    private readonly HttpClient fastHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    private readonly MenuItem fastMenu = new() { IsCheckable = true };
    private CpaFastStatus? fastStatus;
    private CancellationTokenSource? fastRequest;
    private string? fastError;

    private bool HasFastConnection => !settings.UseDemo && !settings.UseLocalAuth &&
        !string.IsNullOrWhiteSpace(managementKey) && Uri.TryCreate(settings.ManagementUrl, UriKind.Absolute, out var uri) &&
        uri.AbsolutePath.TrimEnd('/').EndsWith("/v8/management", StringComparison.OrdinalIgnoreCase);

    private void WireFastMenu()
    {
        fastMenu.Click += async (_, _) => await LoadFastStatusAsync(toggle: true);
        menu.Items.Insert(1, fastMenu);
        menu.Opened += async (_, _) => { if (fastStatus is null) await LoadFastStatusAsync(); };
        Closed += (_, _) => { fastRequest?.Cancel(); fastHttp.Dispose(); };
        ShowFastStatus();
    }

    private void ResetFastStatus()
    {
        fastRequest?.Cancel();
        fastRequest = null;
        fastStatus = null;
        fastError = null;
        ShowFastStatus();
    }

    private async Task LoadFastStatusAsync(bool toggle = false)
    {
        if (!HasFastConnection || fastRequest is not null || toggle && !fastMenu.IsEnabled) return;
        var enabling = fastStatus?.Managed != true;
        using var request = new CancellationTokenSource();
        fastRequest = request;
        ShowFastStatus();
        try
        {
            var source = new CpaFastMode(fastHttp, settings.ManagementUrl, managementKey);
            var status = toggle ? await source.SetAsync(enabling, request.Token) : await source.ReadAsync(request.Token);
            if (fastRequest != request) return;
            fastStatus = status;
            fastError = null;
            if (toggle && enabling && !status.Managed && (status.Conflict || status.Global))
                MessageBox.Show(this, "服务器已有 Fast 规则或档位冲突，未改动现有规则。", "CPA Fast");
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) when (error is HttpRequestException or ArgumentException or JsonException or
            FormatException or InvalidOperationException or OperationCanceledException or IOException)
        {
            if (fastRequest != request) return;
            fastStatus = null;
            fastError = error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }
                ? "管理密钥无权访问" : "服务器状态未确认";
            if (toggle) MessageBox.Show(this, "未能确认 CPA Fast 状态。请按 F5 重新读取后再操作。", "CPA Fast");
        }
        finally
        {
            if (fastRequest == request)
            {
                fastRequest = null;
                if (!request.IsCancellationRequested) ShowFastStatus();
            }
        }
    }

    private void ShowFastStatus()
    {
        fastMenu.Visibility = HasFastConnection ? Visibility.Visible : Visibility.Collapsed;
        var status = fastStatus;
        fastMenu.IsChecked = status?.Global == true;
        Gauge.SetFastIndicator(status?.Global == true ? "FAST" : status?.Partial == true ? "F·部分" : "");
        fastMenu.IsEnabled = fastRequest is null && status is not null &&
            (status.Managed || !status.Global && !status.Conflict);
        var label = fastRequest is not null ? "CPA Fast · 正在同步…" : status switch
        {
            { Managed: true, Conflict: true } => "关闭 CPA Fast · 存在规则冲突",
            { Managed: true } => "关闭 CPA 全局 Fast",
            { Global: true } => "CPA 全局 Fast · 已启用（服务器规则）",
            { Conflict: true } => "CPA Fast · 存在服务器规则冲突",
            { Partial: true } => "开启 CPA 全局 Fast（服务器部分已启用）",
            not null => "开启 CPA 全局 Fast",
            _ => "CPA Fast · " + (fastError ?? "尚未读取")
        };
        fastMenu.Header = FastGlyph.MenuHeader(label);
        AutomationProperties.SetName(fastMenu, label);
    }
}
