namespace CrestApps.Core.ContactCenter;

/// <summary>
/// Told when a caller who was waiting for an agent has hung up.
/// </summary>
/// <remarks>
/// A caller handed from an automated call to a queue is carried on a provider leg the automated voice module
/// owns, and that module deliberately keeps the leg's events away from Contact Center routing — otherwise the
/// routing pipeline would try to reserve an agent for the assistant's own leg. That is right until the moment
/// the caller is handed over, and wrong immediately afterwards: from then on the caller is queue work, and their
/// hanging up is something the queue has to hear about.
/// <para>
/// Without it the queue item stays reserved for somebody who is no longer on the line, the hold music keeps
/// playing to a dead leg, and the call never counts as abandoned — so the number that would have shown callers
/// giving up stays at zero while it happens.
/// </para>
/// <para>
/// An abstraction rather than a direct call because the voice module knows nothing of queues; it is resolved
/// optionally, so automated voice runs perfectly well on a tenant with no Contact Center at all.
/// </para>
/// </remarks>
public interface IQueuedCallerAbandonmentHandler
{
    /// <summary>
    /// Releases the queue work held for a caller who has gone.
    /// </summary>
    /// <param name="activityItemId">The activity the caller was handed over on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CallerAbandonedAsync(string activityItemId, CancellationToken cancellationToken = default);
}
