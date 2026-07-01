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
}
