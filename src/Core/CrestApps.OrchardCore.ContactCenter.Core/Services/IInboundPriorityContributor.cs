using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Notices one reason a particular caller matters more than the queue's default. A contributor answers only for
/// its own reason and returns null when it has nothing to say, so a tenant can add a reason without any
/// contributor needing to know about the others.
/// </summary>
public interface IInboundPriorityContributor
{
    /// <summary>
    /// Gets the order this contributor runs in. It does not affect the outcome — the strongest contribution
    /// wins regardless — but it keeps the recorded reasons in a stable order.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Returns the priority this contributor believes the call deserves, or <see langword="null"/> when it has
    /// no opinion.
    /// </summary>
    /// <param name="context">What is known about the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InteractionPriority?> ContributeAsync(InboundPriorityContext context, CancellationToken cancellationToken = default);
}
