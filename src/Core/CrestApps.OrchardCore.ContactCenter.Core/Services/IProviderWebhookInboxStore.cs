using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for durable provider webhook inbox messages.
/// </summary>
public interface IProviderWebhookInboxStore : ICatalog<ProviderWebhookInboxMessage>
{
    /// <summary>
    /// Finds a message by its canonical provider and provider-scoped delivery identifier.
    /// </summary>
    /// <param name="providerName">The canonical provider technical name.</param>
    /// <param name="deliveryId">The provider-scoped delivery identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching message, or <see langword="null"/> when none exists.</returns>
    Task<ProviderWebhookInboxMessage> FindByDeliveryAsync(
        string providerName,
        string deliveryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists pending messages due for processing, oldest first.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="maxCount">The maximum number of messages to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due inbox messages.</returns>
    Task<IReadOnlyCollection<ProviderWebhookInboxMessage>> ListDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the messages currently in the supplied processing state.
    /// </summary>
    /// <param name="status">The processing state to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages in the supplied state.</returns>
    Task<int> CountByStatusAsync(ProviderWebhookInboxStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the pending or claimed messages whose next attempt is already due at or before the supplied time.
    /// A sustained non-zero result indicates provider ingress is not draining fast enough.
    /// </summary>
    /// <param name="nowUtc">The current UTC time used to select overdue messages.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of overdue messages.</returns>
    Task<int> CountOverdueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
