using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

/// <summary>
/// Synchronizes subscription content item publish state with the Stripe price catalog.
/// </summary>
public sealed class SubscriptionsContentHandler : ContentHandlerBase
{
    private readonly StripePriceSyncService _stripePriceSyncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsContentHandler"/> class.
    /// </summary>
    /// <param name="stripePriceSyncService">The service used to create, update, and unpublish Stripe prices.</param>
    public SubscriptionsContentHandler(StripePriceSyncService stripePriceSyncService)
    {
        _stripePriceSyncService = stripePriceSyncService;
    }

    /// <summary>
    /// Creates or updates the Stripe price for the published subscription content item.
    /// </summary>
    /// <param name="context">The publish context that contains the subscription content item.</param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    public override Task PublishedAsync(PublishContentContext context)
        => _stripePriceSyncService.UpdateOrCreateAsync(context.ContentItem);

    /// <summary>
    /// Marks the Stripe price for the unpublished subscription content item as inactive.
    /// </summary>
    /// <param name="context">The unpublish context that contains the subscription content item.</param>
    /// <returns>A task that represents the asynchronous synchronization operation.</returns>
    public override Task UnpublishedAsync(PublishContentContext context)
        => _stripePriceSyncService.UnpublishAsync(context.ContentItem);
}
