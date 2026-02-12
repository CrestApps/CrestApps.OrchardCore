using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Builds <see cref="OrchestrationContext"/> instances from a given resource object.
/// </summary>
/// <remarks>
/// The default implementation creates an empty context, then executes the
/// registered <see cref="IOrchestrationContextHandler"/> pipeline in the following order:
/// 1) <see cref="IOrchestrationContextHandler.BuildingAsync(OrchestrationContextBuildingContext)"/>,
/// 2) the optional <paramref name="configure"/> delegate,
/// 3) <see cref="IOrchestrationContextHandler.BuiltAsync(OrchestrationContextBuiltContext)"/>.
/// </remarks>
public interface IOrchestrationContextBuilder
{
    /// <summary>
    /// Creates and configures a new <see cref="OrchestrationContext"/> based on the provided <paramref name="resource"/>.
    /// </summary>
    /// <param name="resource">
    /// The resource object (e.g., <see cref="AIProfile"/> or <c>ChatInteraction</c>) used to
    /// seed and configure the orchestration context. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="configure">
    /// An optional delegate to override or fine-tune the context after handlers have run
    /// <c>BuildingAsync</c> but before <c>BuiltAsync</c>.
    /// </param>
    /// <returns>A task that completes with the fully built <see cref="OrchestrationContext"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="resource"/> is <see langword="null"/>.</exception>
    ValueTask<OrchestrationContext> BuildAsync(object resource, Action<OrchestrationContext> configure = null);
}
