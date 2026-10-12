using System.Text.Json;

namespace QuotaFloat;

internal static class CodexFollowPreview
{
    internal static void Run(List<string> report)
    {
        foreach (var enabled in new[] { false, true })
        {
            var options = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings { FollowCodex = enabled }))!;
            var window = new SettingsWindow(options, "fixture-key");
            if (window.FollowCodexOption.IsChecked != enabled || options.FollowCodex != enabled)
                throw new InvalidOperationException("跟随Codex设置序列化与回显失败");
            window.Close();
            report.Add("通过：跟随Codex开关序列化及设置回显：" + enabled);
        }
        if (JsonSerializer.Deserialize<AppSettings>("{}")!.FollowCodex)
            throw new InvalidOperationException("旧设置不应自动开启跟随");
        report.Add("通过：旧设置默认关闭跟随，不突然隐藏独立浮窗");
        if (!CodexClientWindows.IsClientProcess("Codex") || !CodexClientWindows.IsClientProcess("codex") ||
            CodexClientWindows.IsClientProcess("msedge") || CodexClientWindows.IsClientProcess("CodexQuotaFloat") ||
            !CodexClientWindows.IsClientClass("Chrome_WidgetWin_1") ||
            CodexClientWindows.IsClientClass("ConsoleWindowClass"))
            throw new InvalidOperationException("客户端识别须排除CLI控制台、浏览器和浮窗自身");
        report.Add("通过：客户端匹配排除普通浏览器、CLI控制台和浮窗自身");
        var paths = new[] { @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1007.2314.0_x64__test\app\ChatGPT.exe",
            @"C:\Program Files\WindowsApps\OpenAI.ChatGPT_1.0_x64__test\app\ChatGPT.exe", @"C:\Other\ChatGPT.exe" };
        for (var i = 0; i < paths.Length; i++)
        {
            if (CodexClientWindows.IsClientIdentity("ChatGPT", paths[i]) != (i == 0))
                throw new InvalidOperationException("必须区分Codex安装包ChatGPT.exe与普通ChatGPT应用");
            report.Add("通过：Codex安装包身份匹配及普通ChatGPT排除：" + i);
        }
    }
}
