namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides durable, per-handler event idempotency so an at-least-once handler that applies a
/// non-idempotent effect (such as a metrics increment or a workflow trigger) processes each event exactly
/// once even when the event is replayed by outbox retries.
/// </summary>
public interface IContactCenterEventDeduplicationService
{
    /// <summary>
    /// Attempts to reserve one durable event for one handler. When this returns <see langword="true"/> the
    /// caller must apply its effect in the same session so the reservation and the effect commit atomically;
    /// when it returns <see langword="false"/> the event was already processed by this handler and the
    /// effect must be skipped.
    /// </summary>
    /// <param name="handlerId">The stable, versioned identifier of the handler reserving the event.</param>
    /// <param name="eventId">The durable identifier of the event being processed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the event was newly reserved; otherwise <see langword="false"/>.</returns>
    Task<bool> TryBeginAsync(string handlerId, string eventId, CancellationToken cancellationToken = default);
}
