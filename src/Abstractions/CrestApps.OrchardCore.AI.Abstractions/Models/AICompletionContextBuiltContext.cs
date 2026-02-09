namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Carries state after an <see cref="AICompletionContext"/> has been constructed.
/// </summary>
/// <remarks>
/// This context is provided to <c>BuiltAsync</c> handlers in the
/// <see cref="IAICompletionContextBuilderHandler"/> pipeline. At this stage, the <see cref="Context"/>
/// reflects all handler mutations performed during <c>BuildingAsync</c>, and any caller-supplied
/// configuration delegate.
/// </remarks>
public sealed class AICompletionContextBuiltContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionContextBuiltContext"/> class.
    /// </summary>
    /// <param name="resource">The source resource used to build the context.</param>
    /// <param name="context">The finalized <see cref="AICompletionContext"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public AICompletionContextBuiltContext(object resource, AICompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        Resource = resource;
        Context = context;
    }

    /// <summary>
    /// Gets the source resource associated with the built completion context.
    /// </summary>
    public object Resource { get; }

    /// <summary>
    /// Gets the finalized <see cref="AICompletionContext"/>.
    /// </summary>
    public AICompletionContext Context { get; }
}
