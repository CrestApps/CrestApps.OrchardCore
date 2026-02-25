namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Carries state after an <see cref="Models.OrchestrationContext"/> has been constructed.
/// </summary>
/// <remarks>
/// This context is provided to <c>BuiltAsync</c> handlers in the
/// <see cref="IOrchestrationContextBuilderHandler"/> pipeline. At this stage, the <see cref="OrchestrationContext"/>
/// reflects all handler mutations performed during <c>BuildingAsync</c>, and any caller-supplied
/// configuration delegate.
/// </remarks>
public sealed class OrchestrationContextBuiltContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationContextBuiltContext"/> class.
    /// </summary>
    /// <param name="resource">The source resource used to build the context.</param>
    /// <param name="context">The finalized <see cref="Models.OrchestrationContext"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public OrchestrationContextBuiltContext(object resource, OrchestrationContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        Resource = resource;
        OrchestrationContext = context;
    }

    /// <summary>
    /// Gets the source resource associated with the built orchestration context.
    /// </summary>
    public object Resource { get; }

    /// <summary>
    /// Gets the finalized <see cref="Models.OrchestrationContext"/>.
    /// </summary>
    public OrchestrationContext OrchestrationContext { get; }
}
