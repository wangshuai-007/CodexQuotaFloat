using System.Text.Json;

namespace QuotaFloat.Core;

/// <summary>只读Codex文件认证；不保存、记录或刷新OAuth凭据。</summary>
public sealed class LocalCodexAuth
{
    internal string AccessToken { get; }
    internal string AccountId { get; }
    internal string Name { get; }
    internal string Plan { get; }

    private LocalCodexAuth(string token, string accountId, string name, string plan)
    { AccessToken = token; AccountId = accountId; Name = name; Plan = plan; }

    /// <summary>显式文件优先；默认CODEX_HOME/auth.json或用户.codex/auth.json，不扫描其它目录。</summary>
    public static string ResolvePath(string? selected, string? codexHome = null, string? userHome = null)
    {
        var home = codexHome ?? ReadCodexHome();
        var path = string.IsNullOrWhiteSpace(selected) ? Path.Combine(string.IsNullOrWhiteSpace(home) ?
            Path.Combine(userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex") : home, "auth.json") : selected.Trim();
        path = Environment.ExpandEnvironmentVariables(path);
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            path = Path.Combine(userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("认证文件须使用本机磁盘上的完整路径");
        return Path.GetFullPath(path);
    }

    /// <summary>每次额度刷新重新只读文件，Codex换号/更新认证后下次刷新生效。</summary>
    public static async Task<LocalCodexAuth> ReadAsync(string? path, CancellationToken token)
    {
        await using var file = new FileStream(ResolvePath(path), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
        if (file.Length > 1024 * 1024) throw new FormatException("本地认证文件过大");
        var bytes = new byte[1024 * 1024 + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await file.ReadAsync(bytes.AsMemory(count), token);
            if (read == 0) break;
            count += read;
        }
        if (count > 1024 * 1024) throw new FormatException("本地认证文件过大");
        using var json = JsonDocument.Parse(bytes.AsMemory(0, count));
        return Parse(json.RootElement, DateTimeOffset.UtcNow);
    }

    /// <summary>提取ChatGPT登录的访问令牌及工作区标识；API Key不能查询订阅额度。</summary>
    public static LocalCodexAuth Parse(JsonElement root, DateTimeOffset now)
    {
        var mode = CpaParser.Text(root, "auth_mode");
        if (mode is not null && !mode.Equals("chatgpt", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("chatgptAuthTokens", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("本地认证不是ChatGPT登录；API Key等认证不提供订阅额度");
        if (mode is null && !string.IsNullOrWhiteSpace(CpaParser.Text(root, "OPENAI_API_KEY")))
            throw new FormatException("本地Codex使用API Key；请使用ChatGPT登录后再查询订阅额度");
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tokens", out var tokens) ||
            tokens.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(CpaParser.Text(tokens, "access_token")))
            throw new FormatException("本地认证缺少ChatGPT访问令牌；请先在Codex登录");
        var access = CpaParser.Text(tokens, "access_token")!;
        if (access.Any(char.IsWhiteSpace) || access.Any(char.IsControl)) throw new FormatException("本地访问令牌格式无效");
        using var accessClaims = ReadClaims(access);
        using var idClaims = ReadClaims(CpaParser.Text(tokens, "id_token"));
        if (accessClaims is not null && accessClaims.RootElement.TryGetProperty("exp", out var expiry) &&
            expiry.ValueKind == JsonValueKind.Number && expiry.TryGetInt64(out var unix) && unix <= now.ToUnixTimeSeconds())
            throw new FormatException("本地认证已过期；请让Codex更新登录后手动刷新，本工具不改写认证");
        var id = CpaParser.Text(tokens, "account_id") ?? Claim(idClaims, "chatgpt_account_id") ?? Claim(accessClaims, "chatgpt_account_id");
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new FormatException("本地认证缺少有效账号标识；请重新登录Codex");
        RejectFedramp(idClaims); RejectFedramp(accessClaims);
        return new(access, id, CpaParser.Text(idClaims?.RootElement ?? default, "email") ?? "本地Codex账号",
            Claim(idClaims, "chatgpt_plan_type") ?? Claim(accessClaims, "chatgpt_plan_type") ?? "Codex");
    }

    private static JsonDocument? ReadClaims(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt) || jwt.Split('.').Length != 3) return null;
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        try
        {
            var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
            throw new FormatException("认证JWT内容不是对象");
        }
        catch (Exception error) when (error is FormatException or JsonException)
        { throw new FormatException("本地认证JWT格式无效；请重新登录Codex"); }
    }

    private static string? Claim(JsonDocument? document, string field)
    {
        var root = document?.RootElement ?? default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("https://api.openai.com/auth", out var auth)
            ? CpaParser.Text(auth, field) : null;
    }

    private static string? ReadCodexHome()
    {
        var value = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable("CODEX_HOME", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable("CODEX_HOME", EnvironmentVariableTarget.Machine);
        return value;
    }

    private static void RejectFedramp(JsonDocument? document)
    {
        var root = document?.RootElement ?? default;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("https://api.openai.com/auth", out var auth) &&
            auth.ValueKind == JsonValueKind.Object && auth.TryGetProperty("chatgpt_account_is_fedramp", out var flag) && flag.ValueKind == JsonValueKind.True)
            throw new FormatException("该账号需要专用服务地址，当前本地文件模式不支持");
    }
}
