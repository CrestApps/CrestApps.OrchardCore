using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using OrchardCore.Environment.Shell.Configuration;
using Polly;

namespace CrestApps.OrchardCore.Core.Http;

/// <summary>
/// The one egress guard for outbound calls to addresses an administrator, a workflow or a request supplies (webhooks,
/// connectors, calendar subscriptions, push endpoints, self-hosted services): it keeps them off the site's own network, so
/// they cannot be used for server-side request forgery against internal services. It is the only classifier of internal
/// addresses; never write another.
/// </summary>
/// <remarks>
/// An address is checked when it is saved, but a host name can resolve to anything when it is called, so the guarded
/// handler checks the addresses a host resolves to at connection time and refuses the connection when every one of them
/// is internal. A client that follows redirects has every hop checked the same way. By default the handler never goes
/// through a proxy: through one, the connection would be checked against the proxy's address rather than the
/// destination's. A host whose only way out is a proxy sets <see cref="UseSystemProxyKey"/>: the handler then goes
/// through the system proxy, connects to the proxy whatever its address, and checks each destination's own DNS answer
/// before the proxy is asked to reach it.
/// </remarks>
public static class EgressNetwork
{
    /// <summary>
    /// The message of the exception thrown when a connection to the site's own network is refused.
    /// </summary>
    public const string RefusedMessage = "is on the site's own network";

    /// <summary>
    /// The configuration key (<c>true</c> or <c>false</c>, read from the tenant's configuration) that lets the guarded
    /// clients go through the system proxy (<see cref="HttpClient.DefaultProxy"/>: the <c>HTTPS_PROXY</c>,
    /// <c>HTTP_PROXY</c> and <c>NO_PROXY</c> environment variables, or the system's settings on Windows). Off by default.
    /// </summary>
    public const string UseSystemProxyKey = "CrestApps:Egress:UseSystemProxy";

    /// <summary>
    /// Returns whether an address is on the site's own network: loopback, private, link-local, shared (carrier-grade
    /// NAT), IETF protocol assignments (192.0.0.0/24), benchmarking (198.18.0.0/15), multicast, reserved, broadcast or
    /// unspecified, the Azure host's platform address (168.63.129.16), an IPv6 site-local, unique-local or multicast
    /// address, and a NAT64 (64:ff9b::/96) or 6to4 (2002::/16) address whose embedded IPv4 address is any of these. An
    /// address of an unknown family is treated as internal.
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

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) ||
                (bytes[0] == 168 && bytes[1] == 63 && bytes[2] == 129 && bytes[3] == 16) ||
                bytes[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // 64:ff9b::/96 and 2002::/16 translate to IPv4, so the embedded address decides.
            return TryGetEmbeddedIPv4(bytes, out var embedded) && IsInternal(embedded);
        }

        return true;
    }

    /// <summary>
    /// Returns whether a URL names a host on the site's own network by its address or as <c>localhost</c>. A host name
    /// that only resolves to an internal address is caught when it is called, by <see cref="CreateHandler(bool)"/>.
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
    /// network, or plain http to this machine while developing: the rule for receivers that are sent secrets or signed
    /// events. Addresses that may be plain http use <see cref="IsExternalUrl"/>.
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
    /// Returns whether a URL is an absolute http or https address with no user name or password whose host is not on the
    /// site's own network by its address or as <c>localhost</c> (this machine is allowed while developing). It says
    /// nothing about the scheme beyond that: a feature that needs https checks it as a separate rule, with its own
    /// message, and a host name that only resolves to an internal address is refused when it is called.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="isDevelopment">Whether the site runs in the Development environment.</param>
    public static bool IsExternalUrl(string url, bool isDevelopment)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && IsInternal(literal))
        {
            return isDevelopment && IPAddress.IsLoopback(literal);
        }

        return !uri.IsLoopback || isDevelopment;
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
        => CreateHandler(allowLoopback, allowPrivateNetwork, allowRedirects, proxy: null);

    /// <summary>
    /// Creates a handler that checks every address it connects to, redirects included, and goes through a proxy when one
    /// is given. Through the proxy, the proxy is connected to whatever its address (it is the host's), and each
    /// destination is checked by its own DNS answer before the proxy is asked to reach it; every request opens its own
    /// connection, so one proxy connection never carries an unchecked destination. The proxy resolves the name again, so
    /// it must keep its own egress rules against a name that answers differently each time.
    /// </summary>
    /// <param name="allowLoopback">Whether loopback addresses are allowed (development only).</param>
    /// <param name="allowPrivateNetwork">Whether private and loopback addresses are allowed (never link-local).</param>
    /// <param name="allowRedirects">Whether redirects are followed. Each hop is checked.</param>
    /// <param name="proxy">The proxy to go through, such as <see cref="GetProxy(IServiceProvider)"/>, or
    /// <see langword="null"/> for none.</param>
    public static SocketsHttpHandler CreateHandler(bool allowLoopback, bool allowPrivateNetwork, bool allowRedirects, IWebProxy proxy)
    {
        var handler = new SocketsHttpHandler
        {
            // Through a proxy the connect callback sees the proxy's address, not the destination's: ConnectAsync checks the
            // destination on its own then.
            UseProxy = proxy is not null,
            Proxy = proxy,
            AllowAutoRedirect = allowRedirects,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, allowLoopback, allowPrivateNetwork, proxy, cancellationToken),
        };

        if (proxy is not null)
        {
            // A plain-http connection to a proxy carries requests for any destination; not reusing it makes every request
            // connect, so every destination, each redirect hop included, is checked.
            handler.PooledConnectionLifetime = TimeSpan.Zero;
        }

        return handler;
    }

    /// <summary>
    /// Returns the system proxy when the tenant's configuration sets <see cref="UseSystemProxyKey"/> to <c>true</c>, and
    /// otherwise <see langword="null"/>: the guarded clients connect directly.
    /// </summary>
    /// <param name="serviceProvider">The tenant's services.</param>
    public static IWebProxy GetProxy(IServiceProvider serviceProvider)
        => bool.TryParse(serviceProvider?.GetService<IShellConfiguration>()?[UseSystemProxyKey], out var useProxy) && useProxy
            ? HttpClient.DefaultProxy
            : null;

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
    /// Whether the address is link-local (169.254.0.0/16 or fe80::/10, or a NAT64 address embedding 169.254.0.0/16),
    /// where the cloud metadata service answers.
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

        if (bytes.Length == 16)
        {
            return TryGetEmbeddedIPv4(bytes, out var embedded) && IsLinkLocal(embedded);
        }

        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    /// <summary>
    /// Registers a named HTTP client whose connections go through the egress guard. Connections to this machine are
    /// allowed only in the Development environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="configureClient">Configures the client, for example its time-out.</param>
    /// <param name="allowRedirects">Whether redirects are followed, each hop checked at connect, for addresses that commonly
    /// move (calendar feeds, recording downloads). Off by default.</param>
    public static IHttpClientBuilder AddEgressGuardedHttpClient(this IServiceCollection services, string name, Action<HttpClient> configureClient = null, bool allowRedirects = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return AddGuardedClient(services, name, configureClient, allowPrivateNetwork: null, allowRedirects);
    }

    /// <summary>
    /// Registers a named HTTP client whose connections go through the egress guard and whose requests go through the
    /// standard resilience handler: a total and a per-attempt timeout, a circuit breaker, and retries of safe methods
    /// only (a failed POST may have been carried out, so it is never replayed). A connection the guard refuses is not
    /// retried.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the client.</param>
    /// <param name="configureClient">Configures the client, for example its time-out, or <see langword="null"/>.</param>
    /// <param name="configureResilience">Adjusts the resilience options, or <see langword="null"/> for the defaults.</param>
    /// <param name="allowPrivateNetwork">Whether private and loopback addresses are allowed, decided by the host (for a
    /// self-hosted service the host opts in to), or <see langword="null"/> to refuse them. Link-local addresses are
    /// refused even then.</param>
    public static IHttpClientBuilder AddEgressGuardedHttpClient(
        this IServiceCollection services,
        string name,
        Action<HttpClient> configureClient,
        Action<HttpStandardResilienceOptions> configureResilience,
        Func<IServiceProvider, bool> allowPrivateNetwork = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var builder = AddGuardedClient(services, name, configureClient, allowPrivateNetwork, allowRedirects: false);

        builder.AddStandardResilienceHandler(options =>
        {
            var shouldRetry = options.Retry.ShouldHandle;

            options.Retry.ShouldHandle = arguments => IsRefusal(arguments.Outcome.Exception)
                ? PredicateResult.False()
                : shouldRetry(arguments);

            configureResilience?.Invoke(options);

            // Applied last, so no caller can turn it back on.
            options.Retry.DisableForUnsafeHttpMethods();
        });

        return builder;
    }

    private static IHttpClientBuilder AddGuardedClient(IServiceCollection services, string name, Action<HttpClient> configureClient, Func<IServiceProvider, bool> allowPrivateNetwork, bool allowRedirects)
    {
        var builder = configureClient is null
            ? services.AddHttpClient(name)
            : services.AddHttpClient(name, configureClient);

        return builder.ConfigurePrimaryHttpMessageHandler(serviceProvider => CreateHandler(
            serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() == true,
            allowPrivateNetwork?.Invoke(serviceProvider) == true,
            allowRedirects,
            GetProxy(serviceProvider)));
    }

    private static bool IsRefusal(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException && current.Message.Contains(RefusedMessage, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The IPv4 address a NAT64 (64:ff9b::/96, the last four bytes) or 6to4 (2002::/16, bytes two to five) address
    // carries.
    private static bool TryGetEmbeddedIPv4(byte[] bytes, out IPAddress embedded)
    {
        if (bytes.Length == 16 && bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b)
        {
            embedded = new IPAddress(bytes.AsSpan(12, 4));

            return true;
        }

        if (bytes.Length == 16 && bytes[0] == 0x20 && bytes[1] == 0x02)
        {
            embedded = new IPAddress(bytes.AsSpan(2, 4));

            return true;
        }

        embedded = null;

        return false;
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowLoopback, bool allowPrivateNetwork, IWebProxy proxy, CancellationToken cancellationToken)
    {
        var destination = context.InitialRequestMessage?.RequestUri;

        if (proxy is not null && destination is not null && IsProxyConnection(context.DnsEndPoint, destination, proxy))
        {
            // The proxy is the host's, so it is connected to whatever its address; the destination is checked first.
            await ResolveAllowedAsync(destination.IdnHost, allowLoopback, allowPrivateNetwork, cancellationToken);

            return await ConnectAnyAsync(await ResolveAsync(context.DnsEndPoint.Host, cancellationToken), context.DnsEndPoint.Port, cancellationToken);
        }

        var allowed = await ResolveAllowedAsync(context.DnsEndPoint.Host, allowLoopback, allowPrivateNetwork, cancellationToken);

        return await ConnectAnyAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
    }

    // Whether this connection goes to the proxy the request is sent through, rather than to its destination.
    private static bool IsProxyConnection(DnsEndPoint endPoint, Uri destination, IWebProxy proxy)
    {
        if (proxy.IsBypassed(destination) || proxy.GetProxy(destination) is not { } proxyUri)
        {
            return false;
        }

        return string.Equals(proxyUri.IdnHost.Trim('[', ']'), endPoint.Host.Trim('[', ']'), StringComparison.OrdinalIgnoreCase) &&
            proxyUri.Port == endPoint.Port;
    }

    private static async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        host = host.Trim('[', ']');

        return IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
    }

    private static async Task<IPAddress[]> ResolveAllowedAsync(string host, bool allowLoopback, bool allowPrivateNetwork, CancellationToken cancellationToken)
    {
        var allowed = (await ResolveAsync(host, cancellationToken))
            .Where(candidate => !IsRefused(candidate, allowLoopback, allowPrivateNetwork))
            .ToArray();

        if (allowed.Length == 0)
        {
            throw new HttpRequestException($"The host '{host}' {RefusedMessage}.");
        }

        return allowed;
    }

    // Every address is tried in turn, as the default handler does: a host with both an IPv6 and an IPv4 address may only
    // answer on one of them.
    private static async Task<Stream> ConnectAnyAsync(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        Exception lastError = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);

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
