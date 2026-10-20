using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Services;

/// <summary>
/// Keeps webhooks off the site's own network. An address is checked when it's saved, but a hostname can resolve to
/// anything when it's called, so the connection itself is refused when the host resolves only to internal addresses.
/// </summary>
internal static class WebhookNetwork
{
    /// <summary>
    /// Returns whether an address is on the site's own network: loopback, private, link-local or unspecified.
    /// </summary>
    public static bool IsInternal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        return bytes.Length == 4 &&
            (bytes[0] == 10 ||
             bytes[0] == 127 ||
             (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168) ||
             (bytes[0] == 169 && bytes[1] == 254) ||
             bytes[0] == 0);
    }

    /// <summary>
    /// Returns the handler webhook requests are sent through: no redirects, and no connections to the site's own
    /// network, except this machine while developing.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(bool allowLoopback)
        => new()
        {
            AllowAutoRedirect = false,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, allowLoopback, cancellationToken),
        };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowLoopback, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(context.DnsEndPoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(candidate => !IsInternal(candidate) || (allowLoopback && IPAddress.IsLoopback(candidate)))
            ?? throw new HttpRequestException($"The webhook's host '{context.DnsEndPoint.Host}' is on the site's own network.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
        }
    }
}
