using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// The one egress guard for outbound calls to addresses an administrator or a workflow enters (webhooks, connectors):
/// it keeps them off the site's own network, so they cannot be used for server-side request forgery against internal
/// services.
/// </summary>
/// <remarks>
/// An address is checked when it is saved, but a host name can resolve to anything when it is called, so the guarded
/// handler checks the addresses a host resolves to at connection time and refuses the connection when every one of them
/// is internal. Redirects are not followed, so an approved address cannot send the request somewhere else.
/// </remarks>
public static class EgressNetwork
{
    /// <summary>
    /// The message of the exception thrown when a connection to the site's own network is refused.
    /// </summary>
    public const string RefusedMessage = "is on the site's own network";

    /// <summary>
    /// Returns whether an address is on the site's own network: loopback, private, link-local, shared (carrier-grade
    /// NAT), multicast, reserved or unspecified.
    /// </summary>
    /// <param name="address">The address to classify.</param>
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
    /// Returns whether a URL names a host on the site's own network by its address or as <c>localhost</c>. A host name
    /// that only resolves to an internal address is caught when it is called, by <see cref="CreateHandler"/>.
    /// </summary>
    /// <param name="uri">The absolute URL.</param>
    public static bool IsInternalHost(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsLoopback)
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && IsInternal(literal);
    }

    /// <summary>
    /// Returns whether outbound events may be posted to a URL: an absolute https address that is not on the site's own
    /// network, or plain http to this machine while developing.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="isDevelopment">Whether the site runs in the Development environment.</param>
    public static bool IsAllowedUrl(string url, bool isDevelopment)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && IsInternal(literal))
        {
            return isDevelopment && uri.Scheme == Uri.UriSchemeHttp && IPAddress.IsLoopback(literal);
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return !uri.IsLoopback;
        }

        return isDevelopment &&
            uri.Scheme == Uri.UriSchemeHttp &&
            uri.IsLoopback;
    }

    /// <summary>
    /// Returns a handler that follows no redirects and refuses connections to the site's own network, except to this
    /// machine when <paramref name="allowLoopback"/> is set (while developing).
    /// </summary>
    /// <param name="allowLoopback">Whether connections to this machine are allowed.</param>
    public static SocketsHttpHandler CreateHandler(bool allowLoopback)
        => new()
        {
            AllowAutoRedirect = false,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, allowLoopback, cancellationToken),
        };

    /// <summary>
    /// Registers a named HTTP client whose connections go through the egress guard. Connections to this machine are
    /// allowed only in the Development environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="configureClient">Configures the client, for example its time-out.</param>
    public static IHttpClientBuilder AddEgressGuardedHttpClient(this IServiceCollection services, string name, Action<HttpClient> configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var builder = configureClient is null
            ? services.AddHttpClient(name)
            : services.AddHttpClient(name, configureClient);

        return builder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            CreateHandler(serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() == true));
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowLoopback, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host.Trim('[', ']');
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        var address = addresses.FirstOrDefault(candidate => !IsInternal(candidate) || (allowLoopback && IPAddress.IsLoopback(candidate)))
            ?? throw new HttpRequestException($"The host '{context.DnsEndPoint.Host}' {RefusedMessage}.");

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
