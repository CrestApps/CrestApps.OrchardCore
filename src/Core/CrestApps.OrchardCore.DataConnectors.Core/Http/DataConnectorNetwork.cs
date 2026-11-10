using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.DataConnectors.Http;

/// <summary>
/// Keeps connectors off the site's own network unless the configuration allows it. A host is checked when it is
/// called, on the addresses it resolves to then, so a name that resolves to a public address when it is saved and to
/// an internal one later is still refused.
/// </summary>
internal static class DataConnectorNetwork
{
    /// <summary>
    /// Returns whether an address is on the site's own network, as the shared <see cref="EgressNetwork"/> guard
    /// classifies it.
    /// </summary>
    public static bool IsInternal(IPAddress address)
        => EgressNetwork.IsInternal(address);

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
    public static Task<IPAddress[]> ResolveAllowedAsync(string host, DataConnectorNetworkOptions options, CancellationToken cancellationToken)
    {
        return ResolveAllowedAsync(host, options, logger: null, cancellationToken);
    }

    /// <summary>
    /// Resolves a host and returns the addresses a connector may connect to. A refused host is counted on
    /// <c>crestapps.dataconnector.network.blocked</c> and logged as a warning, without the host.
    /// </summary>
    /// <exception cref="DataConnectorException">Every address of the host is on the site's own network.</exception>
    public static async Task<IPAddress[]> ResolveAllowedAsync(string host, DataConnectorNetworkOptions options, ILogger logger, CancellationToken cancellationToken)
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
            // A connection that tries to reach the site's own network can be an attempt to probe internal services.
            DataConnectorsTelemetry.Add(DataConnectorsTelemetry.NetworkBlocked);
            logger?.NetworkBlocked();

            throw new DataConnectorException($"The host '{trimmed}' is on the site's own network. To reach it, add it to CrestApps:DataConnectors:Network:AllowedHosts or set AllowPrivateNetworks in the site's configuration.");
        }

        return allowed;
    }

    /// <summary>
    /// Returns the handler HTTP-based connectors send requests through: no automatic redirects, and no connections to
    /// the site's own network unless the configuration allows them.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(DataConnectorNetworkOptions options, ILogger logger = null)
        => new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, options, logger, cancellationToken),
        };

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        DataConnectorNetworkOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;

        try
        {
            addresses = await ResolveAllowedAsync(context.DnsEndPoint.Host, options, logger, cancellationToken);
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
