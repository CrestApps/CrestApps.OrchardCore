namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

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
    /// Processes a normalized serialized payload.
    /// </summary>
    /// <param name="payload">The normalized serialized payload.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
