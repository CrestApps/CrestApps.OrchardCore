using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// The one place a request's address becomes a throttling key. An IPv6 client is keyed by its /64, because a client
/// rotates addresses inside it and would otherwise never repeat a key. The address comes from Orchard Core's rate-limit
/// helpers, so a site behind a proxy must enable Orchard's Reverse Proxy feature for it to be the client's.
/// </summary>
public static class ClientNetworkKey
{
    /// <summary>
    /// The key of a request whose address is unknown.
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// The network of the request: the IPv4 address, or the IPv6 address masked to /64. Payment limits use this even for
    /// a signed-in buyer, because a card tester can register an account.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public static string NetworkOnly(HttpContext httpContext)
    {
        if (httpContext is null)
        {
            return Unknown;
        }

        var raw = RateLimitPartitionHelpers.GetRemoteIpAddress(httpContext);

        return FromAddress(raw);
    }

    /// <summary>
    /// The client of the request: the signed-in user, otherwise the network.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public static string From(HttpContext httpContext)
    {
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var key = RateLimitPartitionHelpers.GetClientPartitionKey(httpContext);

            if (!string.Equals(key, RateLimitPartitionHelpers.GetRemoteIpAddress(httpContext), StringComparison.Ordinal))
            {
                return key;
            }
        }

        return NetworkOnly(httpContext);
    }

    /// <summary>
    /// The network key of an address string.
    /// </summary>
    /// <param name="address">The address.</param>
    public static string FromAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || !IPAddress.TryParse(address, out var ip))
        {
            return Unknown;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }

        var bytes = ip.GetAddressBytes();

        for (var i = 8; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }

        return new IPAddress(bytes) + "/64";
    }
}
