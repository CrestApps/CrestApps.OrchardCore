using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Pushes a product to the ACP feed APIs after it is published, so agents see its new price, text and photos.
/// </summary>
internal sealed class AgentFeedProductHandler : ContentHandlerBase
{
    private readonly AgentFeedChangeTracker _tracker;

    public AgentFeedProductHandler(AgentFeedChangeTracker tracker)
    {
        _tracker = tracker;
    }

    public override Task PublishedAsync(PublishContentContext context)
    {
        if (context.ContentItem.Has<ProductPart>())
        {
            _tracker.Add(context.ContentItem.ContentItemId);
        }

        return Task.CompletedTask;
    }
}
