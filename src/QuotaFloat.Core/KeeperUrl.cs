using System.Net;

namespace QuotaFloat.Core;

/// <summary>Keeper根地址校验；HTTPS优先，局域网明文必须显式允许。</summary>
public static class KeeperUrl
{
    /// <summary>保留部署子路径，拒绝用户信息、查询参数和非Web协议。</summary>
    public static Uri Validate(string address, bool allowLanHttp)
    {
        if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("请填写Keeper应用根地址");
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var url) ||
            url.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(url.UserInfo) ||
            !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
            throw new ArgumentException("请输入Keeper应用根地址，不包含密码、查询参数或片段");
        if (url.AbsolutePath.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Keeper地址应为应用根地址，不是api接口地址");
        if (url.Scheme == "http" && !url.IsLoopback && !(allowLanHttp && IsPrivateIp(url.Host)))
            throw new ArgumentException("Keeper请使用HTTPS；局域网私有IP的HTTP须明确勾选允许明文传输");
        return new Uri(url.AbsoluteUri.TrimEnd('/') + "/");
    }

    private static bool IsPrivateIp(string host)
    {
        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return false;
        var bytes = ip.GetAddressBytes();
        return bytes.Length == 4 ? bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 192 && bytes[1] == 168 : bytes.Length == 16 && (bytes[0] & 0xfe) == 0xfc;
    }
}
