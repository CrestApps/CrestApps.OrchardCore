using CrestApps.Core.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.WebSockets;

/// <summary>
/// Registers the registry that knows where a live socket is attached.
/// </summary>
public static class WebSocketServiceCollectionExtensions
{
    /// <summary>
    /// Adds the per-node connection registry to the suite.
    /// </summary>
    /// <param name="builder">The suite builder.</param>
    /// <returns>The same suite builder, so calls can be chained.</returns>
    public static CrestAppsContactCenterSuiteBuilder AddWebSockets(this CrestAppsContactCenterSuiteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddCoreWebSockets();

        return builder;
    }

    /// <summary>
    /// Adds the per-node connection registry.
    /// </summary>
    /// <remarks>
    /// Per-node is correct for one node and wrong the moment there are two: a provider callback that lands on a
    /// node which did not open the socket has to be answerable anyway. A host that runs more than one node
    /// registers <see cref="DistributedWebSocketConnectionRegistry"/> over a store the nodes share, either
    /// before this call or by replacing the descriptor afterwards.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreWebSockets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IWebSocketConnectionRegistry, InMemoryWebSocketConnectionRegistry>();

        return services;
    }
}
