using CrestApps.Core.Services;
using CrestApps.OrchardCore.Commerce.Webhooks.Indexes;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Services;

public interface IWebhookSubscriptionStore : ICatalog<WebhookSubscription>
{
    Task<IReadOnlyList<WebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<WebhookSubscription> Subscriptions)> PageAsync(int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class WebhookSubscriptionStore : DocumentCatalog<WebhookSubscription, WebhookSubscriptionIndex>, IWebhookSubscriptionStore
{
    public WebhookSubscriptionStore(ISession session)
        : base(session)
    {
        CollectionName = WebhookConstants.CollectionName;
    }

    public async Task<IReadOnlyList<WebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => [.. await Session.Query<WebhookSubscription, WebhookSubscriptionIndex>(index => index.IsEnabled, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<(int Count, IReadOnlyList<WebhookSubscription> Subscriptions)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<WebhookSubscription, WebhookSubscriptionIndex>(collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}

public interface IWebhookDeliveryStore : ICatalog<WebhookDelivery>
{
    /// <summary>
    /// Returns pending deliveries whose next attempt is due, oldest first.
    /// </summary>
    Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of a subscription's deliveries, newest first, and how many match.
    /// </summary>
    Task<(int Count, IReadOnlyList<WebhookDelivery> Deliveries)> PageAsync(string subscriptionId, WebhookDeliveryStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes delivered and failed deliveries made before a date, up to a number at a time. Returns how many.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default);
}

public sealed class WebhookDeliveryStore : DocumentCatalog<WebhookDelivery, WebhookDeliveryIndex>, IWebhookDeliveryStore
{
    public WebhookDeliveryStore(ISession session)
        : base(session)
    {
        CollectionName = WebhookConstants.CollectionName;
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListDueAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
    {
        var pending = (int)WebhookDeliveryStatus.Pending;

        return [.. await Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.Status == pending && index.NextAttemptUtc <= utcNow, collection: CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(take)
            .ListAsync(cancellationToken)];
    }

    public async Task<(int Count, IReadOnlyList<WebhookDelivery> Deliveries)> PageAsync(string subscriptionId, WebhookDeliveryStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.SubscriptionId == subscriptionId, collection: CollectionName);

        if (status is { } wanted)
        {
            var value = (int)wanted;
            query = query.Where(index => index.Status == value);
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime createdBeforeUtc, int take, CancellationToken cancellationToken = default)
    {
        var pending = (int)WebhookDeliveryStatus.Pending;
        var old = await Session.Query<WebhookDelivery, WebhookDeliveryIndex>(index => index.Status != pending && index.CreatedUtc < createdBeforeUtc, collection: CollectionName)
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
