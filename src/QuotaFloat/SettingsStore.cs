using System.Text.Json;

namespace QuotaFloat;

/// <summary>仅保存界面选项和CPA地址；管理密钥不会写入文件。</summary>
public sealed class AppSettings
{
    public bool UseDemo { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool DockToEdge { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public int DemoCount { get; set; } = 3;
    public int RefreshSeconds { get; set; } = 60;
    public string ManagementUrl { get; set; } = "http://127.0.0.1:8317/v8/management";
    public double? Left { get; set; }
    public double? Top { get; set; }
}

/// <summary>原子保存本地设置；异常交给界面处理，不隐藏损坏配置。</summary>
public static class SettingsStore
{
    public static string PathName { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaFloat", "settings.json");

    /// <summary>首次启动使用演示；损坏或不可读文件返回默认值并提示。</summary>
    public static (AppSettings Settings, string? Warning) Load()
    {
        if (!File.Exists(PathName)) return (new(), null);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathName))
                ?? throw new JsonException();
            settings.DemoCount = Math.Clamp(settings.DemoCount, 0, 100);
            settings.RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 30, 3600);
            if (settings.Left is { } x && !double.IsFinite(x)) settings.Left = null;
            if (settings.Top is { } y && !double.IsFinite(y)) settings.Top = null;
            return (settings, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new(), "本地设置无法读取，已使用默认演示设置；请检查设置文件权限或格式。");
        }
    }

    /// <summary>UTF-8无BOM写入后原子替换；不包含任何账号凭据。</summary>
    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        var temporary = PathName + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }),
            new System.Text.UTF8Encoding(false));
        File.Move(temporary, PathName, true);
    }
}
