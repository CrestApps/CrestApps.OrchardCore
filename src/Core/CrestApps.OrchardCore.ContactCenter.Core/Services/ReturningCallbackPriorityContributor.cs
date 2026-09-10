using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Raises a caller who is returning a callback we placed. They already waited once, gave up their place, and
/// were promised a call; putting them at the back of the line when they ring us back turns a courtesy into an
/// insult.
/// </summary>
public sealed class ReturningCallbackPriorityContributor : IInboundPriorityContributor
{
    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public Task<InteractionPriority?> ContributeAsync(InboundPriorityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult<InteractionPriority?>(context.IsReturningCallback
            ? InteractionPriority.Highest
            : null);
    }
}
