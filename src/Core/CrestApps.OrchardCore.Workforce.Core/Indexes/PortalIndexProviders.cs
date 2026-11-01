using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Maps portal inbox items to <see cref="WorkforceInboxIndex"/> rows.
/// </summary>
public sealed class WorkforceInboxIndexProvider : IndexProvider<WorkforceInboxItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceInboxIndexProvider"/> class.
    /// </summary>
    public WorkforceInboxIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WorkforceInboxItem> context)
    {
        context.For<WorkforceInboxIndex>()
            .Map(item => new WorkforceInboxIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                IsRead = item.ReadUtc.HasValue,
                CreatedUtc = item.CreatedUtc,
            });
    }
}

/// <summary>
/// Maps web push subscriptions to <see cref="WebPushSubscriptionIndex"/> rows.
/// </summary>
public sealed class WebPushSubscriptionIndexProvider : IndexProvider<WebPushSubscription>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebPushSubscriptionIndexProvider"/> class.
    /// </summary>
    public WebPushSubscriptionIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WebPushSubscription> context)
    {
        context.For<WebPushSubscriptionIndex>()
            .Map(item => new WebPushSubscriptionIndex
            {
                ItemId = item.ItemId,
                WorkerId = item.WorkerId,
                EndpointHash = PortalSecrets.Hash(item.Endpoint),
            });
    }
}

/// <summary>
/// Maps a worker's feed secret to <see cref="WorkerFeedIndex"/> rows.
/// </summary>
public sealed class WorkerFeedIndexProvider : IndexProvider<WorkerProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerFeedIndexProvider"/> class.
    /// </summary>
    public WorkerFeedIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<WorkerProfile> context)
    {
        context.For<WorkerFeedIndex>()
            .Map(worker => worker.TryGet<WorkerPortalSettings>(out var settings) && !string.IsNullOrEmpty(settings.FeedToken)
                ? new WorkerFeedIndex { WorkerId = worker.ItemId, TokenHash = PortalSecrets.Hash(settings.FeedToken) }
                : null);
    }
}

/// <summary>
/// The index providers of the portal.
/// </summary>
public static class PortalIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
    {
        return
        [
            new WorkforceInboxIndexProvider(),
            new WebPushSubscriptionIndexProvider(),
            new WorkerFeedIndexProvider(),
        ];
    }
}
