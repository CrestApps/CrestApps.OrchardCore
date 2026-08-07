using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for the durable interaction event history.
/// </summary>
public interface IInteractionEventStore : ICatalog<InteractionEvent>
{
    /// <summary>
    /// Lists the events recorded for the specified interaction, oldest first.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The ordered list of events for the interaction.</returns>
    Task<IReadOnlyList<InteractionEvent>> ListByInteractionAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an event with the specified idempotency key has already been recorded.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to check.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a matching event exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a bounded batch of events that occurred strictly before the supplied cutoff, oldest first.
    /// </summary>
    /// <param name="cutoffUtc">The exclusive UTC cutoff; events older than this are returned.</param>
    /// <param name="maxCount">The maximum number of events to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The batch of expired events.</returns>
    Task<IReadOnlyList<InteractionEvent>> ListOlderThanAsync(DateTime cutoffUtc, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the events of the supplied types recorded against the supplied aggregate type up to and including
    /// the supplied instant, oldest first. Reporting reads the event log through this method rather than
    /// querying it directly so that a stored payload written by an earlier release is brought to the current
    /// schema version before the report deserializes it.
    /// </summary>
    /// <param name="aggregateType">The aggregate type the events were recorded against.</param>
    /// <param name="eventTypes">The event types to include. When empty, every event type is included.</param>
    /// <param name="occurredThroughUtc">The inclusive UTC upper bound on occurrence time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching events, oldest first.</returns>
    Task<IReadOnlyList<InteractionEvent>> ListByAggregateTypeAsync(
        string aggregateType,
        IEnumerable<string> eventTypes,
        DateTime occurredThroughUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of events ordered deterministically by occurrence time then identifier. It is the
    /// forward-only enumeration used to replay the entire event log during a projection rebuild or drift
    /// check; callers page until fewer than <paramref name="take"/> events are returned.
    /// </summary>
    /// <param name="skip">The number of events to skip.</param>
    /// <param name="take">The maximum number of events to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The requested page of events, oldest first.</returns>
    Task<IReadOnlyList<InteractionEvent>> ListOrderedPageAsync(int skip, int take, CancellationToken cancellationToken = default);
}
