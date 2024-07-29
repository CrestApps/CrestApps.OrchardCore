using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.CrestApps.Subscriptions.Core.Models;
using YesSql.Indexes;

namespace OrchardCore.CrestApps.Subscriptions.Core.Indexes;

public sealed class SubscriptionsContentItemIndexProvider : IndexProvider<ContentItem>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionsContentItemIndexProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<SubscriptionsContentItemIndex>()
            .When(x => x.Published)
            .Map(async contentItem =>
            {
                var part = contentItem.As<SubscriptionsPart>();

                if (part == null)
                {
                    return null;
                }

                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

                if (definition?.StereotypeEquals(SubscriptionsConstants.Stereotype) == false)
                {
                    return null;
                }

                return new SubscriptionsContentItemIndex()
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentType = contentItem.ContentType,
                    Sort = part.Sort ?? 0,
                };
            });
            .
    }
}
