namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Processes one normalized payload from the durable provider webhook inbox.
/// </summary>
public interface IProviderWebhookInboxHandler
{
    /// <summary>
    /// Gets the stable technical name used to route persisted payloads to this handler.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets the machine-readable replay contract for this handler. Provider webhook delivery is
    /// at-least-once, so this value states honestly how the handler stays idempotent when the same
    /// persisted payload is dispatched again. Registration validation rejects an
    /// <see cref="ContactCenterHandlerReplaySafety.Unspecified"/> contract.
    /// </summary>
    ContactCenterHandlerReplaySafety ReplaySafety { get; }

    /// <summary>
    /// Processes a normalized serialized payload.
    /// </summary>
    /// <param name="payload">The normalized serialized payload.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
