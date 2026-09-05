using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves the priority an inbound call is queued at, combining the configured priority with whatever the
/// contributors notice about the caller.
/// </summary>
public interface IInboundPriorityResolver
{
    /// <summary>
    /// Resolves the priority to enqueue at.
    /// </summary>
    /// <param name="context">What is known about the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InteractionPriority> ResolveAsync(InboundPriorityContext context, CancellationToken cancellationToken = default);
}
