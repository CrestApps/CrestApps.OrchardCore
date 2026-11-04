using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores web push subscriptions.
/// </summary>
internal sealed class WebPushSubscriptionStore : DocumentCatalog<WebPushSubscription, WebPushSubscriptionIndex>
{
    public WebPushSubscriptionStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<WebPushSubscription>> ListAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<WebPushSubscription, WebPushSubscriptionIndex>(index => index.WorkerId == workerId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    public Task<WebPushSubscription> FindAsync(string workerId, string endpoint, CancellationToken cancellationToken = default)
    {
        var hash = PortalSecrets.Hash(endpoint);

        return Session.Query<WebPushSubscription, WebPushSubscriptionIndex>(index => index.WorkerId == workerId && index.EndpointHash == hash, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
