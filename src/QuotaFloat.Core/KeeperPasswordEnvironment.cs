namespace QuotaFloat.Core;

/// <summary>读取Keeper专用登录密码，不使用CPA管理密钥，不写入文件。</summary>
public static class KeeperPasswordEnvironment
{
    /// <summary>按进程、Windows用户、系统顺序读取；保留密码原有空格。</summary>
    public static string Read()
    {
        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var value = Environment.GetEnvironmentVariable("KEEPER_LOGIN_PASSWORD", target);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return "";
    }
}
