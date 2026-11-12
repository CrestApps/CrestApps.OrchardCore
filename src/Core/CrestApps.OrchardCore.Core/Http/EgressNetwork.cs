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
        => CreateHandler(allowLoopback, allowPrivateNetwork: false, allowRedirects: false);

    /// <summary>
    /// Creates a handler that checks every address it connects to, redirects included, so a redirect or a DNS answer that
    /// leads to an internal address is refused at connect.
    /// </summary>
    /// <param name="allowLoopback">Whether loopback addresses are allowed (development only).</param>
    /// <param name="allowPrivateNetwork">Whether private and loopback addresses are allowed, for hosts that run local
    /// services on purpose (a local model host). Link-local addresses, the cloud metadata service among them, are refused
    /// even then.</param>
    /// <param name="allowRedirects">Whether redirects are followed. Each hop is checked at connect.</param>
    public static SocketsHttpHandler CreateHandler(bool allowLoopback, bool allowPrivateNetwork, bool allowRedirects)
        => new()
        {
            AllowAutoRedirect = allowRedirects,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, allowLoopback, allowPrivateNetwork, cancellationToken),
        };

    /// <summary>
    /// Whether a connection to the address is refused.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="allowLoopback">Whether loopback addresses are allowed.</param>
    /// <param name="allowPrivateNetwork">Whether private and loopback addresses are allowed (never link-local).</param>
    public static bool IsRefused(IPAddress address, bool allowLoopback, bool allowPrivateNetwork)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (!IsInternal(address))
        {
            return false;
        }

        if (IsLinkLocal(address))
        {
            return true;
        }

        return !allowPrivateNetwork && !(allowLoopback && IPAddress.IsLoopback(address));
    }

    /// <summary>
    /// Whether the address is link-local (169.254.0.0/16 or fe80::/10), where the cloud metadata service answers.
    /// </summary>
    /// <param name="address">The address.</param>
    public static bool IsLinkLocal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.IsIPv6LinkLocal)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

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
        => await ConnectAsync(context, allowLoopback, allowPrivateNetwork: false, cancellationToken);

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowLoopback, bool allowPrivateNetwork, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host.Trim('[', ']');
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        var allowed = addresses.Where(candidate => !IsRefused(candidate, allowLoopback, allowPrivateNetwork)).ToList();

        if (allowed.Count == 0)
        {
            throw new HttpRequestException($"The host '{context.DnsEndPoint.Host}' {RefusedMessage}.");
        }

        // Every allowed address is tried in turn, as the default handler does: a host with both an IPv6 and an IPv4
        // address may only answer on one of them. Each address was checked above, so none of them is internal.
        Exception lastError = null;

        foreach (var address in allowed)
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
                lastError = exception;
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        }

        throw lastError;
    }
}
