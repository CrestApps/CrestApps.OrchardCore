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
    /// exponential back-off or dead-lettering based on the outcome. Each due message is processed in its
    /// own fresh Orchard child scope so a poison message or a canceled YesSql session cannot block the
    /// remaining messages in the batch.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages that were successfully redelivered and removed.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes one due outbox message by its identifier. This is the per-message unit of work that
    /// <see cref="DispatchDueAsync"/> runs in an isolated child scope; it reloads the message, re-runs its
    /// incomplete handlers, and either removes the message or schedules a retry.
    /// </summary>
    /// <param name="messageId">The durable outbox message identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the message completed and was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> DispatchDueMessageAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one registered handler for an event. The outbox runs this operation in an isolated child
    /// scope so the handler effect and any replay marker commit together, while an exception rolls both
    /// back without canceling the message-dispatch session.
    /// </summary>
    /// <param name="interactionEvent">The event to handle.</param>
    /// <param name="handlerId">The stable identifier of the handler to execute.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DispatchHandlerAsync(
        InteractionEvent interactionEvent,
        string handlerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a claimed message in a fresh scope after handler execution, rejecting stale owners by fence.
    /// </summary>
    /// <param name="messageId">The durable outbox message identifier.</param>
    /// <param name="ownerToken">The owner token captured when the message was claimed.</param>
    /// <param name="fenceToken">The fence token captured when the message was claimed.</param>
    /// <param name="completedHandlerIds">The stable identifiers of handlers that completed.</param>
    /// <param name="error">The first handler error, or <see langword="null"/> when all handlers completed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the message completed; otherwise <see langword="false"/>.</returns>
    Task<bool> SettleClaimAsync(
        string messageId,
        string ownerToken,
        long fenceToken,
        IReadOnlyCollection<string> completedHandlerIds,
        string error,
        CancellationToken cancellationToken = default);
}
