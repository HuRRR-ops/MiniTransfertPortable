using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MiniTransfertPortable;

internal static class NetworkAddress
{
    private static readonly string[] PublicIpServices =
    [
        "https://api.ipify.org",
        "https://checkip.amazonaws.com"
    ];

    public static async Task<IPAddress?> GetPublicIpAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var service in PublicIpServices)
        {
            try
            {
                var text = (await client.GetStringAsync(service, cancellationToken)).Trim();
                if (IPAddress.TryParse(text, out var address))
                {
                    return address;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Try the next independent public-IP service.
            }
        }
        return null;
    }

    public static IPAddress? GetLocalIp()
    {
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var properties = network.GetIPProperties();
            if (!properties.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork))
            {
                continue;
            }

            var address = properties.UnicastAddresses
                .Select(item => item.Address)
                .FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(item));
            if (address is not null)
            {
                return address;
            }
        }
        return null;
    }

    public static string UrlHost(IPAddress address) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
}
