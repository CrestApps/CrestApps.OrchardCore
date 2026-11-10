using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Stores webhook deliveries in the <see cref="WebhookStorage.CollectionName"/> collection.
/// </summary>
public sealed class WebhookDeliveryStore : DocumentCatalog<WebhookDelivery, WebhookDeliveryIndex>, IWebhookDeliveryStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    public WebhookDeliveryStore(ISession session)
        : base(session)
    {
        CollectionName = WebhookStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
    {
        var pending = WebhookDeliveryStatus.Pending;

        return [.. await Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.Status == pending && index.NextAttemptUtc <= utcNow, collection: CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken)];
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = WebhookDeliveryStatus.Pending;

        return Session.QueryIndex<WebhookDeliveryIndex>(index => index.Status == pending, collection: CollectionName)
            .CountAsync(cancellationToken);
    }

    public async Task<(int Count, IReadOnlyList<WebhookDelivery> Deliveries)> PageAsync(string subscriptionId, WebhookDeliveryStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.SubscriptionId == subscriptionId, collection: CollectionName);

        if (status is { } wanted)
        {
            query = query.Where(index => index.Status == wanted);
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBySubscriptionAsync(string subscriptionId, int take, CancellationToken cancellationToken = default)
    {
        var deliveries = await Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.SubscriptionId == subscriptionId, collection: CollectionName)
            .Take(take)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var delivery in deliveries)
        {
            Session.Delete(delivery, CollectionName);
            count++;
        }

        return count;
    }

    /// <inheritdoc/>
    public Task<int> DeleteOlderThanAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default)
        => DeleteOlderThanAsync(createdBeforeUtc, take, [], cancellationToken);

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime createdBeforeUtc, int take, IReadOnlyCollection<string> keptEventNames, CancellationToken cancellationToken = default)
    {
        var pending = WebhookDeliveryStatus.Pending;
        var query = Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.Status != pending && index.CreatedUtc < createdBeforeUtc, collection: CollectionName);

        if (keptEventNames is { Count: > 0 })
        {
            var kept = keptEventNames.ToArray();

            query = query.Where(index => index.EventName.IsNotIn(kept));
        }

        var old = await query
            .Take(take)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var delivery in old)
        {
            Session.Delete(delivery, CollectionName);
            count++;
        }

        return count;
    }
}
