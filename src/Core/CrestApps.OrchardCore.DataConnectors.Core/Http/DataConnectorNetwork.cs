using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Keeps connectors off the site's own network unless the configuration allows it. A host is checked when it is
/// called, on the addresses it resolves to then, so a name that resolves to a public address when it is saved and to
/// an internal one later is still refused.
/// </summary>
internal static class DataConnectorNetwork
{
    /// <summary>
    /// Returns whether an address is on the site's own network: loopback, private, link-local, shared (carrier-grade
    /// NAT) or unspecified.
    /// </summary>
    public static bool IsInternal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6UniqueLocal ||
            address.IsIPv6Multicast ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        return bytes.Length == 4 &&
            (bytes[0] == 0 ||
             bytes[0] == 10 ||
             bytes[0] == 127 ||
             (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
             (bytes[0] == 169 && bytes[1] == 254) ||
             (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168) ||
             bytes[0] >= 224);
    }

    /// <summary>
    /// Returns whether the configuration names a host as one connectors may call on any network.
    /// </summary>
    public static bool IsAllowedHost(string host, DataConnectorNetworkOptions options)
    {
        if (string.IsNullOrEmpty(host) || options?.AllowedHosts is not { Length: > 0 } allowed)
        {
            return false;
        }

        var trimmed = host.Trim().Trim('[', ']');

        return allowed.Any(entry => !string.IsNullOrWhiteSpace(entry) && string.Equals(entry.Trim().Trim('[', ']'), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves a host and returns the addresses a connector may connect to.
    /// </summary>
    /// <exception cref="DataConnectorException">Every address of the host is on the site's own network.</exception>
    public static async Task<IPAddress[]> ResolveAllowedAsync(string host, DataConnectorNetworkOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        var trimmed = host.Trim().Trim('[', ']');
        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(trimmed, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(trimmed, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new DataConnectorException($"The host '{trimmed}' couldn't be found.", exception) { IsTransient = true };
        }

        if (options?.AllowPrivateNetworks == true || IsAllowedHost(trimmed, options))
        {
            return addresses;
        }

        var allowed = addresses.Where(address => !IsInternal(address)).ToArray();

        if (allowed.Length == 0)
        {
            throw new DataConnectorException($"The host '{trimmed}' is on the site's own network. To reach it, add it to CrestApps:DataConnectors:Network:AllowedHosts or set AllowPrivateNetworks in the site's configuration.");
        }

        return allowed;
    }

    /// <summary>
    /// Returns the handler HTTP-based connectors send requests through: no automatic redirects, and no connections to
    /// the site's own network unless the configuration allows them.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(DataConnectorNetworkOptions options)
        => new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, options, cancellationToken),
        };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, DataConnectorNetworkOptions options, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;

        try
        {
            addresses = await ResolveAllowedAsync(context.DnsEndPoint.Host, options, cancellationToken);
        }
        catch (DataConnectorException exception)
        {
            throw new HttpRequestException(exception.Message, exception);
        }

        Exception last = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                last = exception;
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        }

        throw new HttpRequestException($"Couldn't connect to '{context.DnsEndPoint.Host}'.", last);
    }
}
