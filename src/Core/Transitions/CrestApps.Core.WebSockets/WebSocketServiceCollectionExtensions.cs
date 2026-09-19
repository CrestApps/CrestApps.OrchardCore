using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.WebSockets;

/// <summary>
/// Registers the registry that knows where a live socket is attached.
/// </summary>
public static class WebSocketServiceCollectionExtensions
{
    /// <summary>
    /// Adds the per-node connection registry.
    /// </summary>
    /// <remarks>
    /// Per-node is correct for one node and wrong the moment there are two: a provider callback that lands on a
    /// node which did not open the socket has to be answerable anyway. A host that runs more than one node
    /// replaces this with <see cref="DistributedWebSocketConnectionRegistry"/> over a store the nodes share.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreWebSockets(this IServiceCollection services)
    {
        services.AddSingleton<IWebSocketConnectionRegistry, InMemoryWebSocketConnectionRegistry>();

        return services;
    }
}
