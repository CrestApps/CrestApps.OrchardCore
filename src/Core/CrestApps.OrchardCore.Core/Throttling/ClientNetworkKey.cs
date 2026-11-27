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

    private const string AgentPlatformPrefix = "agent-platform:";

    private const string AgentBuyerSeparator = "#buyer:";

    /// <summary>
    /// The key of an agent platform's requests, for limits where the request names no client of its own: every buyer an
    /// agent platform brings comes from the platform's servers, or (over MCP or A2A) from no known address at all. Keyed
    /// by the platform, one platform's misuse never locks out another.
    /// </summary>
    /// <param name="platformId">The agent platform's id.</param>
    public static string ForAgentPlatform(string platformId)
        => string.IsNullOrWhiteSpace(platformId) ? Unknown : AgentPlatformPrefix + platformId;

    /// <summary>
    /// The key of one buyer an agent platform brings: the platform plus something that names the buyer, such as the agent
    /// checkout. Limits tuned for one client network (wrong discount codes, declined cards) count per buyer with it, so
    /// one buyer's declines don't stop every buyer of a busy platform; <see cref="GetAgentPlatform(string)"/> gives the
    /// platform's own key back for the much higher limits the platform as a whole has.
    /// </summary>
    /// <param name="platformId">The agent platform's id.</param>
    /// <param name="buyerKey">What names the buyer, or <see langword="null"/> for the platform's key.</param>
    public static string ForAgentBuyer(string platformId, string buyerKey)
        => string.IsNullOrWhiteSpace(buyerKey) || string.IsNullOrWhiteSpace(platformId)
            ? ForAgentPlatform(platformId)
            : AgentPlatformPrefix + platformId + AgentBuyerSeparator + buyerKey;

    /// <summary>
    /// The platform's key of an agent buyer's key (<see cref="ForAgentBuyer(string, string)"/>), or <see langword="null"/>
    /// for any other key.
    /// </summary>
    /// <param name="key">A throttling key.</param>
    public static string GetAgentPlatform(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(AgentPlatformPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var separator = key.LastIndexOf(AgentBuyerSeparator, StringComparison.Ordinal);

        return separator > AgentPlatformPrefix.Length ? key[..separator] : null;
    }

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
    /// The site the request comes from, for a daily allowance one holder must not multiply: the IPv4 address, or the IPv6
    /// address masked to /48, the block one site is usually given, since a holder of a /48 has 65,536 /64 networks.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public static string Site(HttpContext httpContext)
    {
        var network = NetworkOnly(httpContext);

        if (!network.EndsWith("/64", StringComparison.Ordinal) || !IPAddress.TryParse(network[..^3], out var ip))
        {
            return network;
        }

        var bytes = ip.GetAddressBytes();

        for (var i = 6; i < bytes.Length; i++)
        {
            bytes[i] = 0;
        }

        return new IPAddress(bytes) + "/48";
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
    /// The network key of an address string. A key this class made already is given back unchanged, so a caller that
    /// is handed a key rather than an address can't turn every IPv6 network into <see cref="Unknown"/>.
    /// </summary>
    /// <param name="address">The address, or a network key.</param>
    public static string FromAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return Unknown;
        }

        if (address.StartsWith(AgentPlatformPrefix, StringComparison.Ordinal) && address.Length > AgentPlatformPrefix.Length)
        {
            return address;
        }

        // An IPv6 key ends with its /64 prefix length, which IPAddress.TryParse refuses.
        if (address.EndsWith("/64", StringComparison.Ordinal))
        {
            address = address[..^3];

            if (!IPAddress.TryParse(address, out var network) || network.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return Unknown;
            }
        }

        if (!IPAddress.TryParse(address, out var ip))
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
