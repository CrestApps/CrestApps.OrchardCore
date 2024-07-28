using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.CrestApps.Subscriptions.Core.Models;

namespace OrchardCore.CrestApps.Subscriptions.Handlers;
public class SubscriptionContentHandler : ContentHandlerBase
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionContentHandler(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override Task InitializingAsync(InitializingContentContext context)
    {
        if (IsSubscriptions(context.ContentItem?.ContentType))
        {
            context.ContentItem.Weld<SubscriptionsPart>();
        }

        return Task.CompletedTask;
    }

    private bool IsSubscriptions(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var contentTypeDefinition = _contentDefinitionManager.GetTypeDefinitionAsync(contentType).GetAwaiter().GetResult();

        return contentTypeDefinition?.StereotypeEquals("Subscriptions") ?? false;
    }
}
