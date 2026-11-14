using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Decides whether an endpoint an administrator types for an AI connection, agent, crawler or provider may be saved.
/// </summary>
public interface IAIEndpointPolicy
{
    /// <summary>
    /// Whether the endpoint may be saved: off the site's own network, unless the host allows a private network.
    /// </summary>
    /// <param name="url">The endpoint.</param>
    bool IsAllowed(string url);
}

/// <inheritdoc/>
/// <remarks>
/// Besides the text of the address, the host name is resolved and refused when any address it answers with is on the
/// site's own network. The model provider SDKs build their own transport, which the connect-time guard does not cover, so
/// this is the check they get; a name that resolves differently later is still only caught at connect by the guarded
/// clients. A name that does not resolve now is allowed, since a private resolver may answer it only when it is called.
/// </remarks>
public sealed class AIEndpointPolicy : IAIEndpointPolicy
{
    private readonly bool _isDevelopment;
    private readonly AIEgressOptions _options;
    private readonly Func<string, IPAddress[]> _resolve;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIEndpointPolicy"/> class.
    /// </summary>
    public AIEndpointPolicy(IHostEnvironment hostEnvironment, IOptions<AIEgressOptions> options)
        : this(hostEnvironment, options, Dns.GetHostAddresses)
    {
    }

    internal AIEndpointPolicy(IHostEnvironment hostEnvironment, IOptions<AIEgressOptions> options, Func<string, IPAddress[]> resolve)
    {
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _options = options.Value;
        _resolve = resolve;
    }

    /// <inheritdoc/>
    public bool IsAllowed(string url)
        => AIEgress.IsAllowedEndpoint(url, _isDevelopment, _options) && !ResolvesInward(url);

    private bool ResolvesInward(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.IdnHost.Trim('[', ']');

        // An address literal was judged by its text already.
        if (IPAddress.TryParse(host, out _))
        {
            return false;
        }

        IPAddress[] addresses;

        try
        {
            addresses = _resolve(host);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return false;
        }

        var allowPrivateNetwork = _options?.AllowPrivateNetwork == true;

        return addresses.Any(address => EgressNetwork.IsRefused(address, allowLoopback: _isDevelopment, allowPrivateNetwork));
    }
}
