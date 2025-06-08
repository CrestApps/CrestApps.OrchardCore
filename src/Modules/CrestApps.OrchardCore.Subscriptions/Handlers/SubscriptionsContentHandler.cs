using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

public sealed class SubscriptionsContentHandler : ContentHandlerBase
{
    private readonly StripePriceSyncService _stripePriceSyncService;

    public SubscriptionsContentHandler(StripePriceSyncService stripePriceSyncService)
    {
        _stripePriceSyncService = stripePriceSyncService;
    }

    public override Task PublishedAsync(PublishContentContext context)
        => _stripePriceSyncService.UpdateOrCreateAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context)
        => _stripePriceSyncService.UnpublishAsync(context.ContentItem);
}
