using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Handles lifecycle events raised while building an <see cref="AICompletionContext"/>.
/// </summary>
/// <remarks>
/// Implementations can enrich, validate, or otherwise mutate the context. The builder will invoke
/// <see cref="BuildingAsync(AICompletionContextBuildingContext)"/> first, then apply any caller-provided
/// configuration, and finally invoke <see cref="BuiltAsync(AICompletionContextBuiltContext)"/>.
/// Handlers are resolved from DI and executed in reverse registration order to allow last-registered
/// handlers to run first.
/// </remarks>
public interface IAICompletionContextBuilderHandler
{
    /// <summary>
    /// Called while the <see cref="AICompletionContext"/> is being constructed, before the optional caller
    /// configuration delegate is applied.
    /// </summary>
    /// <param name="context">Carries both the source <see cref="AIProfile"/> and the mutable <see cref="AICompletionContext"/>.</param>
    /// <returns>A task that completes when the mutation or validation is done.</returns>
    Task BuildingAsync(AICompletionContextBuildingContext context);

    /// <summary>
    /// Called after the context has been fully constructed and the optional caller configuration delegate
    /// has been applied.
    /// </summary>
    /// <param name="context">Carries the final <see cref="AICompletionContext"/> along with the source <see cref="AIProfile"/>.</param>
    /// <returns>A task that completes when post-build processing is done.</returns>
    Task BuiltAsync(AICompletionContextBuiltContext context);
}
