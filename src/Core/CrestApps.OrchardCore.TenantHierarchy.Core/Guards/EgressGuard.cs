using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Refuses outbound requests from a parent or child tenant to internal addresses and to the tenants of this
/// application, so a tenant administrator cannot use a feature that sends requests (workflows, media import, remote
/// deployment) to probe other tenants. The check runs on the resolved address when the connection opens, so DNS
/// rebinding cannot get around it.
/// </summary>
public sealed class EgressGuard
{
    private readonly ShellSettings _shellSettings;
    private readonly IShellHost _shellHost;
    private readonly TenantHierarchyEgressOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EgressGuard"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="options">The tenant hierarchy options.</param>
    /// <param name="logger">The logger.</param>
    public EgressGuard(
        ShellSettings shellSettings,
        IShellHost shellHost,
        IOptions<TenantHierarchyOptions> options,
        ILogger<EgressGuard> logger)
    {
        _shellSettings = shellSettings;
        _shellHost = shellHost;
        _options = options.Value.Egress ?? new TenantHierarchyEgressOptions();
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the guard applies to the current tenant.
    /// </summary>
    public bool IsActive => _options.Enabled && _shellSettings.IsInTenantHierarchy();

    /// <summary>
    /// Opens a connection for <see cref="SocketsHttpHandler.ConnectCallback"/> after the host and its addresses pass the guard.
    /// </summary>
    /// <param name="context">The connection context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.DnsEndPoint.Host;

        // When the connection opens to a proxy, the delegating handler has already checked the real destination.
        var destination = context.InitialRequestMessage?.RequestUri;
        var isDestination = destination is null || string.Equals(destination.Host, host, StringComparison.OrdinalIgnoreCase);

        if (isDestination)
        {
            EnsureHostAllowed(host, context.DnsEndPoint.Port);
        }

        IPAddress[] addresses;

        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }

        if (isDestination)
        {
            EnsureAddressesAllowed(host, addresses);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Throws when a request to the host and its resolved addresses must be refused.
    /// </summary>
    /// <param name="host">The host name of the request.</param>
    /// <param name="port">The port of the request.</param>
    /// <param name="addresses">The resolved addresses.</param>
    internal void EnsureAllowed(string host, int port, IReadOnlyCollection<IPAddress> addresses)
    {
        EnsureHostAllowed(host, port);
        EnsureAddressesAllowed(host, addresses);
    }

    /// <summary>
    /// Throws when the host of a request is the host of a tenant of this application. It needs no DNS lookup.
    /// </summary>
    /// <param name="host">The host name of the request.</param>
    /// <param name="port">The port of the request.</param>
    internal void EnsureHostAllowed(string host, int port)
    {
        if (!IsActive || IsAllowListed(host))
        {
            return;
        }

        if (IsTenantHost(host, port))
        {
            Refuse(host, "it is the address of a tenant of this application");
        }
    }

    /// <summary>
    /// Throws when a resolved address of a request is an internal address.
    /// </summary>
    /// <param name="host">The host name of the request.</param>
    /// <param name="addresses">The resolved addresses.</param>
    internal void EnsureAddressesAllowed(string host, IReadOnlyCollection<IPAddress> addresses)
    {
        if (!IsActive || IsAllowListed(host))
        {
            return;
        }

        if (_options.BlockPrivateNetworks && addresses.Any(EgressAddressClassifier.IsInternal))
        {
            Refuse(host, "it resolves to an internal network address");
        }
    }

    private bool IsAllowListed(string host)
    {
        return _options.AllowedHosts?.Any(allowed => string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private bool IsTenantHost(string host, int port)
    {
        var allSettings = _shellHost is GuardedShellHost guarded
            ? guarded.Inner.GetAllSettings()
            : _shellHost.GetAllSettings();

        foreach (var settings in allSettings)
        {
            foreach (var tenantHost in settings.RequestUrlHosts)
            {
                if (MatchesHost(tenantHost, host, port))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether a tenant host, which may carry a port or a leading <c>*.</c> wildcard, matches a request host.
    /// </summary>
    /// <param name="tenantHost">The tenant host.</param>
    /// <param name="host">The request host.</param>
    /// <param name="port">The request port.</param>
    internal static bool MatchesHost(string tenantHost, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            return false;
        }

        var name = tenantHost.Trim();
        var separator = name.LastIndexOf(':');

        if (separator > 0 && int.TryParse(name.AsSpan(separator + 1), out var tenantPort))
        {
            if (tenantPort != port)
            {
                return false;
            }

            name = name.Substring(0, separator);
        }

        if (name.StartsWith("*.", StringComparison.Ordinal))
        {
            return host.EndsWith(name.Substring(1), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(name, host, StringComparison.OrdinalIgnoreCase);
    }

    private void Refuse(string host, string reason)
    {
        _logger.LogWarning(
            "The tenant hierarchy refused an outbound request from tenant '{TenantName}' to '{Host}' because {Reason}.",
            _shellSettings.Name,
            host,
            reason);

        throw new HttpRequestException($"The outbound request to '{host}' was refused because {reason}.");
    }
}
