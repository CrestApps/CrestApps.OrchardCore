using System.Net;
using System.Net.Http;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// The egress guard for the clients a search or model SDK builds with its own transport (Azure AI Search, Elasticsearch),
/// so an endpoint is checked when it is connected to, not only when it is saved: a name saved while it resolved to a
/// public address and pointed at the site's own network later is refused (review 11, C14). It applies the host's
/// <see cref="AIEgressOptions"/>, and the system proxy where the tenant's configuration sets
/// <see cref="EgressNetwork.UseSystemProxyKey"/>.
/// </summary>
public sealed class AIEgressTransport : IDisposable
{
    private readonly bool _isDevelopment;
    private readonly bool _allowPrivateNetwork;
    private readonly IWebProxy _proxy;
    private readonly Lazy<SocketsHttpHandler> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIEgressTransport"/> class.
    /// </summary>
    /// <param name="options">The host's AI egress options.</param>
    /// <param name="hostEnvironment">The host environment, where development allows loopback.</param>
    /// <param name="shellConfiguration">The tenant's configuration, which may let the clients use the system proxy.</param>
    public AIEgressTransport(IOptions<AIEgressOptions> options, IHostEnvironment hostEnvironment = null, IShellConfiguration shellConfiguration = null)
    {
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _allowPrivateNetwork = options?.Value.AllowPrivateNetwork == true;
        _proxy = EgressNetwork.GetProxy(shellConfiguration);
        _handler = new Lazy<SocketsHttpHandler>(() => EgressNetwork.CreateHandler(_isDevelopment, _allowPrivateNetwork, allowRedirects: false, _proxy));
    }

    /// <summary>
    /// Gets a transport that refuses the site's own network unless the host allows a private network, for a client
    /// built without the container: the strictest rule.
    /// </summary>
    public static AIEgressTransport Strict { get; } = new(Options.Create(new AIEgressOptions()));

    /// <summary>
    /// Gets the shared guarded handler, for an SDK transport that takes one. It lives as long as this transport.
    /// </summary>
    public HttpMessageHandler Handler => _handler.Value;

    /// <summary>
    /// Puts the guard on a handler an SDK built, keeping its TLS settings (such as a pinned certificate fingerprint).
    /// </summary>
    /// <param name="handler">The handler.</param>
    /// <param name="allowPrivateNetwork">Whether this endpoint may be on a private network even where the host does not
    /// allow one, as a self-managed cluster saved before the save check was (review 8, G7). Link-local addresses and the
    /// cloud metadata services are refused either way.</param>
    public void Guard(SocketsHttpHandler handler, bool allowPrivateNetwork = false)
        => EgressNetwork.Guard(handler, _isDevelopment, allowPrivateNetwork || _allowPrivateNetwork, _proxy);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_handler.IsValueCreated)
        {
            _handler.Value.Dispose();
        }
    }
}
