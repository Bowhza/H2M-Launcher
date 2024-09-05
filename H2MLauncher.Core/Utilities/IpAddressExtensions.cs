using System.Net;

namespace H2MLauncher.Core;

public static class IpAddressExtensions
{
    public static IPAddress GetRealAddress(this IPAddress ipAddress)
    {
        return ipAddress.IsIPv4MappedToIPv6
                    ? ipAddress.MapToIPv4()
                    : ipAddress;
    }
}
