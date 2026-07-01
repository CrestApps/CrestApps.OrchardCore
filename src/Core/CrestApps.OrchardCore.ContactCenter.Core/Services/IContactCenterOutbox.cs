using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Dispatches Contact Center domain events to their handlers and guarantees at-least-once delivery: a
/// handler failure during inline dispatch is captured as a durable outbox message and retried with
/// exponential back-off until it succeeds or is dead-lettered. Handlers must therefore be idempotent.
/// </summary>
public interface IContactCenterOutbox
{
    /// <summary>
    /// Runs every registered handler for the event inline. When one or more handlers fail, a durable
    /// outbox message is scheduled so the event is retried later instead of being silently lost.
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
