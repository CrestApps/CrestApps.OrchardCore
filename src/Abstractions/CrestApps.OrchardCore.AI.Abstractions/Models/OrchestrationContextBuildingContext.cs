namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Carries state while an <see cref="OrchestrationContext"/> is being constructed.
/// </summary>
/// <remarks>
/// This context is provided to <c>BuildingAsync</c> handlers in the
/// <see cref="IOrchestrationContextHandler"/> pipeline, allowing implementations to enrich,
/// validate, or otherwise modify the mutable <see cref="Context"/> based on the source
/// <see cref="Resource"/>.
/// </remarks>
public sealed class OrchestrationContextBuildingContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationContextBuildingContext"/> class.
    /// </summary>
    /// <param name="resource">The source resource driving the build.</param>
    /// <param name="context">The mutable <see cref="OrchestrationContext"/> being built.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public OrchestrationContextBuildingContext(object resource, OrchestrationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        Resource = resource;
        Context = context;
    }

    /// <summary>
    /// Gets the source resource used to seed and configure the orchestration context.
    /// </summary>
    public object Resource { get; }

    /// <summary>
    /// Gets the mutable <see cref="OrchestrationContext"/> being built. Handlers may mutate this instance.
    /// </summary>
    public OrchestrationContext Context { get; }
}
