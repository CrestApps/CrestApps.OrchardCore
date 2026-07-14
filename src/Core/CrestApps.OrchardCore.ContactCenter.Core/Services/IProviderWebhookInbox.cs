using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Durably accepts authenticated provider webhook deliveries and dispatches persisted payloads with retry recovery.
/// </summary>
public interface IProviderWebhookInbox
{
    /// <summary>
    /// Commits an authenticated normalized delivery before any state-changing handler runs.
    /// </summary>
    /// <param name="delivery">The normalized delivery to accept.</param>
    /// <param name="cancellationToken">The token to monitor until the durable commit completes.</param>
    /// <returns>The acceptance result.</returns>
    Task<ProviderWebhookInboxAcceptanceResult> AcceptAsync(
        ProviderWebhookInboxDelivery delivery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to process one persisted inbox message.
    /// </summary>
    /// <param name="messageId">The durable inbox message identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the message completed and was removed; otherwise <see langword="false"/>.</returns>
    Task<bool> DispatchAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the pending inbox messages whose retry time is due.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages completed and removed.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
