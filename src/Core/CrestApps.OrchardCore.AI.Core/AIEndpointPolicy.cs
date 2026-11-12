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
public sealed class AIEndpointPolicy : IAIEndpointPolicy
{
    private readonly bool _isDevelopment;
    private readonly AIEgressOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIEndpointPolicy"/> class.
    /// </summary>
    public AIEndpointPolicy(IHostEnvironment hostEnvironment, IOptions<AIEgressOptions> options)
    {
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public bool IsAllowed(string url)
        => AIEgress.IsAllowedEndpoint(url, _isDevelopment, _options);
}
