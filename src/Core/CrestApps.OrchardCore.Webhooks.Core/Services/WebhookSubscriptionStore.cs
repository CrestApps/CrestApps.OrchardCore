using CrestApps.OrchardCore.Webhooks.Core.Indexes;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Stores webhook subscriptions in the <see cref="WebhookStorage.CollectionName"/> collection.
/// </summary>
public sealed class WebhookSubscriptionStore : DocumentCatalog<WebhookSubscription, WebhookSubscriptionIndex>, IWebhookSubscriptionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookSubscriptionStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    public WebhookSubscriptionStore(ISession session)
        : base(session)
    {
        CollectionName = WebhookStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => [.. await Session.Query<WebhookSubscription, WebhookSubscriptionIndex>(index => index.IsEnabled, collection: CollectionName).ListAsync(cancellationToken)];

    /// <inheritdoc/>
    public async Task<(int Count, IReadOnlyList<WebhookSubscription> Subscriptions)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<WebhookSubscription, WebhookSubscriptionIndex>(collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}
