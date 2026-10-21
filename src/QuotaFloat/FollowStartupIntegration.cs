using System.Text.Json;
using System.Windows;
using QuotaFloat.Core;

namespace QuotaFloat;

/// <summary>将跟随开关与会话启动检查关联；离线预览不会改用户钩子。</summary>
internal static class FollowStartupIntegration
{
    internal const string TrustNotice = "已配置会话启动检查。请在 Codex 客户端设置 → Hooks → User config 中审核并信任 CodexQuotaFloat，并开启开关；未信任前不会自动启动。仅打开首页不保证触发。";

    internal static bool Apply(bool enabled) => CodexSessionHook.Configure(enabled,
        Path.Combine(AppContext.BaseDirectory, "CodexQuotaFloat.exe"));

    internal static bool IsConfigurationError(Exception error) => error is
        IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException;
}

public partial class MainWindow
{
    private bool ApplyFollowStartup(bool enabled, bool notify = true)
    {
        if (!persistSettings) return true;
        try
        {
            if (FollowStartupIntegration.Apply(enabled) && enabled)
            {
                if (notify) MessageBox.Show(this, FollowStartupIntegration.TrustNotice, "Codex 会话启动检查",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                else tray.ShowBalloonTip(8000, "Codex 会话启动检查", FollowStartupIntegration.TrustNotice,
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            return true;
        }
        catch (Exception error) when (FollowStartupIntegration.IsConfigurationError(error))
        {
            MessageBox.Show(this, "无法更新 Codex 的 hooks.json，启动检查未配置。请检查目录权限及JSON格式，其他钩子不会被覆盖。",
                "Codex 会话启动检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
