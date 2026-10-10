namespace QuotaFloat.Core;

/// <summary>解析管理密钥，不依赖资源管理器是否已更新环境，也不保存或记录明文。</summary>
public static class ManagementKeyResolver
{
    /// <summary>依次读取进程、Windows用户和系统变量，保留进程级显式覆盖。</summary>
    public static string Read() => Resolve(target =>
        Environment.GetEnvironmentVariable("CPA_MANAGEMENT_KEY", target));

    /// <summary>按Process=0（进程）、User=1（用户）、Machine=2（系统）优先级读取；空白值跳过，读取异常上报调用方。</summary>
    public static string Resolve(Func<EnvironmentVariableTarget, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        foreach (var target in new[] { EnvironmentVariableTarget.Process,
                     EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var key = read(target);
            if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
        }
        return "";
    }
}
