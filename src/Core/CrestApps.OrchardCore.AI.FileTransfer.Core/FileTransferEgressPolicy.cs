using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// Keeps the FTP and SFTP servers (and an SFTP proxy) a tenant's administrator names off the site's own network, as the
/// shared <see cref="EgressNetwork"/> guard classifies it: when the connection is saved, and again before every
/// connection, on the addresses the name resolves to then. The host's operator lets a tenant reach its own network with
/// <see cref="FileTransferHostOptions.AllowPrivateNetwork"/>; link-local addresses and the cloud metadata services are
/// refused even then. In development, this machine is allowed.
/// </summary>
public sealed class FileTransferEgressPolicy
{
    private readonly bool _isDevelopment;
    private readonly bool _allowPrivateNetwork;
    private readonly bool _resolvesNames;
    private readonly Func<string, IPAddress[]> _resolve;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransferEgressPolicy"/> class.
    /// </summary>
    /// <param name="options">What the host's operator allows.</param>
    /// <param name="hostEnvironment">The host environment, where development allows this machine.</param>
    public FileTransferEgressPolicy(IOptions<FileTransferHostOptions> options, IHostEnvironment hostEnvironment = null)
        : this(options, hostEnvironment, resolve: null)
    {
    }

    internal FileTransferEgressPolicy(IOptions<FileTransferHostOptions> options, IHostEnvironment hostEnvironment, Func<string, IPAddress[]> resolve, bool resolvesNames = true)
    {
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _allowPrivateNetwork = options?.Value.AllowPrivateNetwork == true;
        _resolve = resolve;
        _resolvesNames = resolvesNames;
    }

    /// <summary>
    /// Gets a value indicating whether the host's operator lets connections reach the site's own network.
    /// </summary>
    public bool AllowPrivateNetwork => _allowPrivateNetwork;

    /// <summary>
    /// Returns a policy that checks addresses and <c>localhost</c> as typed and resolves no name, for an editor built
    /// without the container. The connection is still checked when it is made.
    /// </summary>
    /// <param name="options">What the host's operator allows, or <see langword="null"/>.</param>
    public static FileTransferEgressPolicy AddressesOnly(IOptions<FileTransferHostOptions> options)
        => new(options, hostEnvironment: null, resolve: null, resolvesNames: false);

    /// <summary>
    /// Whether a server may be used: an address that is not on the site's own network, or a name every address of
    /// which is not. A name that does not resolve is refused, since it cannot be checked. An empty host is left to the
    /// other validation.
    /// </summary>
    /// <param name="host">The host name or address.</param>
    public bool IsAllowed(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var trimmed = host.Trim().Trim('[', ']');

        if (!_resolvesNames &&
            !IPAddress.TryParse(trimmed, out _) &&
            !string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return EgressNetwork.IsAllowedHost(trimmed, _isDevelopment, _allowPrivateNetwork, _resolve);
    }

    /// <summary>
    /// Checks a server just before a connection to it, on the addresses its name resolves to now.
    /// </summary>
    /// <param name="host">The host name or address.</param>
    /// <exception cref="InvalidOperationException">The server is on the site's own network.</exception>
    public void EnsureAllowed(string host)
    {
        if (!IsAllowed(host))
        {
            throw Refused(host);
        }
    }

    /// <summary>
    /// Checks a server just before a connection to it and returns the address to connect to, so the connection reaches
    /// the address that was checked rather than whatever the name resolves to a moment later (review 3). A name that
    /// does not resolve now is refused, since nothing could connect to it.
    /// </summary>
    /// <param name="host">The host name or address.</param>
    /// <returns>The checked address, or <see langword="null"/> when no host is given.</returns>
    /// <exception cref="InvalidOperationException">The server is on the site's own network, or could not be found.</exception>
    public IPAddress ResolveAllowed(string host)
    {
        var trimmed = host?.Trim().Trim('[', ']');

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(trimmed, out var literal)
                ? [literal]
                : (_resolve ?? Dns.GetHostAddresses)(trimmed);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            throw Refused(host);
        }

        if (addresses is not { Length: > 0 } || addresses.Any(address => EgressNetwork.IsRefused(address, _isDevelopment, _allowPrivateNetwork)))
        {
            throw Refused(host);
        }

        return addresses[0];
    }

    private static InvalidOperationException Refused(string host)
        => new($"The host '{host?.Trim()}' {EgressNetwork.RefusedMessage}, or could not be found. To reach the site's own network, the host's operator sets {FileTransferHostOptions.SectionName}:{nameof(FileTransferHostOptions.AllowPrivateNetwork)}.");
}
