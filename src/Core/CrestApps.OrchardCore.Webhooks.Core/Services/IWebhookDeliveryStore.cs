using CrestApps.Core.Services;
using CrestApps.OrchardCore.Webhooks.Core.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Stores webhook deliveries: the delivery outbox and its log.
/// </summary>
public interface IWebhookDeliveryStore : ICatalog<WebhookDelivery>
{
    /// <summary>
    /// Lists the pending deliveries whose next attempt is due, oldest first.
    /// </summary>
    /// <param name="utcNow">The current time.</param>
    /// <param name="take">How many to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The due deliveries.</returns>
    Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of a subscription's deliveries, newest first.
    /// </summary>
    /// <param name="subscriptionId">The subscription.</param>
    /// <param name="status">Only deliveries in this status, or <see langword="null"/> for all.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many there are, and the page.</returns>
    Task<(int Count, IReadOnlyList<WebhookDelivery> Deliveries)> PageAsync(string subscriptionId, WebhookDeliveryStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes delivered and failed deliveries queued before a time. Pending deliveries are kept.
    /// </summary>
    /// <param name="createdBeforeUtc">Deliveries queued before this time are deleted.</param>
    /// <param name="take">The most one call deletes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription's deliveries.
    /// </summary>
    /// <param name="subscriptionId">The subscription.</param>
    /// <param name="take">The most one call deletes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteBySubscriptionAsync(string subscriptionId, int take, CancellationToken cancellationToken = default);
}
