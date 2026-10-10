using QuotaFloat.Core;

internal static class ManagementKeyTests
{
    internal static int Run()
    {
        var passed = 0;
        void Check(string label, bool condition)
        {
            if (!condition) throw new InvalidOperationException("失败：" + label);
            Console.WriteLine("通过：" + label);
            passed++;
        }
        string Resolve(string? process, string? user, string? machine) =>
            ManagementKeyResolver.Resolve(target => target switch
            {
                EnvironmentVariableTarget.Process => process,
                EnvironmentVariableTarget.User => user,
                _ => machine
            });
        Check("双击启动旧进程环境为空时读取用户密钥", Resolve(null, "test-user", "test-machine") == "test-user");
        Check("用户变量为空时读取系统密钥", Resolve("", null, "test-machine") == "test-machine");
        Check("进程密钥保留显式覆盖优先级", Resolve("test-process", "test-user", "test-machine") == "test-process");
        Check("跳过空白变量", Resolve("  ", "\t", "test-machine") == "test-machine");
        Check("未配置时返回空字符串", Resolve(null, null, null) == "");
        Check("移除误复制的首尾空白", Resolve(null, " test-user ", null) == "test-user");
        var calls = 0;
        Check("进程有值时不读取持久环境", ManagementKeyResolver.Resolve(_ =>
            { calls++; return "test-process"; }) == "test-process" && calls == 1);
        var denied = false;
        try { ManagementKeyResolver.Resolve(_ => throw new System.Security.SecurityException()); }
        catch (System.Security.SecurityException) { denied = true; }
        Check("读取权限错误不伪装成未配置", denied);
        return passed;
    }
}
