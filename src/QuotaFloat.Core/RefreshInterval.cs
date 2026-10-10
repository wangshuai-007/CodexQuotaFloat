namespace QuotaFloat.Core;

/// <summary>所有刷新配置使用同一范围，防止界面与持久化夹紧不一致。</summary>
public static class RefreshInterval
{
    public const int MinimumSeconds = 5;
    public const int MaximumSeconds = 3600;

    /// <summary>读取旧设置或外部设置时限制到5–3600秒。</summary>
    public static int Clamp(int seconds) => Math.Clamp(seconds, MinimumSeconds, MaximumSeconds);

    /// <summary>界面输入必须为范围内整数，不静默替换用户输入。</summary>
    public static bool IsValid(int seconds) => seconds is >= MinimumSeconds and <= MaximumSeconds;
}
