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
}
