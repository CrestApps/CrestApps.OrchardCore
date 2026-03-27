namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Carries state while an <see cref="AICompletionContext"/> is being constructed.
/// </summary>
/// <remarks>
/// This context is provided to <c>BuildingAsync</c> handlers in the
/// <see cref="IAICompletionContextBuilderHandler"/> pipeline, allowing implementations to enrich,
/// validate, or otherwise modify the mutable <see cref="Context"/> based on the source
/// <see cref="Profile"/>.
/// </remarks>
public sealed class AICompletionContextBuildingContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionContextBuildingContext"/> class.
    /// </summary>
    /// <param name="profile">The source <see cref="AIProfile"/> driving the build.</param>
    /// <param name="context">The mutable <see cref="AICompletionContext"/> being built.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public AICompletionContextBuildingContext(AIProfile profile, AICompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        Profile = profile;
        Context = context;
    }

    /// <summary>
    /// Gets the source <see cref="AIProfile"/> used to seed and configure the completion context.
    /// </summary>
    public AIProfile Profile { get; }

    /// <summary>
    /// Gets the mutable <see cref="AICompletionContext"/> being built. Handlers may mutate this instance.
    /// </summary>
    public AICompletionContext Context { get; }
}
