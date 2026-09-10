using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

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
    /// Dispatches one normalized payload to its feature-scoped handler in an isolated shell scope.
    /// </summary>
    /// <param name="handlerName">The stable technical name of the handler.</param>
    /// <param name="payload">The normalized serialized payload.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DispatchHandlerAsync(
        string handlerName,
        string payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a claimed delivery in a fresh scope after handler execution, rejecting stale owners by fence.
    /// </summary>
    /// <param name="messageId">The durable inbox message identifier.</param>
    /// <param name="ownerToken">The owner token captured when the message was claimed.</param>
    /// <param name="fenceToken">The fence token captured when the message was claimed.</param>
    /// <param name="succeeded">Whether the handler completed successfully.</param>
    /// <param name="errorType">The handler exception type name when execution failed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the message completed; otherwise <see langword="false"/>.</returns>
    Task<bool> SettleClaimAsync(
        string messageId,
        string ownerToken,
        long fenceToken,
        bool succeeded,
        string errorType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the pending inbox messages whose retry time is due.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages completed and removed.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
