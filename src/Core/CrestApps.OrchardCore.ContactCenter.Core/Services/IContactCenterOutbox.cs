using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Dispatches Contact Center domain events to their handlers and guarantees at-least-once delivery.
/// Every event is durably enqueued before dispatch and remains pending until all handlers complete or
/// the message is dead-lettered. Handlers must therefore be idempotent.
/// </summary>
public interface IContactCenterOutbox
{
    /// <summary>
    /// Durably enqueues an event for handler dispatch.
    /// </summary>
    /// <param name="interactionEvent">The event to enqueue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EnqueueAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs every incomplete registered handler for an already-enqueued event. Successful delivery
    /// removes the outbox message; failures are retried with exponential back-off.
    /// </summary>
    /// <param name="interactionEvent">The event to dispatch.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DispatchAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the outbox messages that are due for retry, re-running their handlers and applying
    /// exponential back-off or dead-lettering based on the outcome.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages that were successfully redelivered and removed.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
