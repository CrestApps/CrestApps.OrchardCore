using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.CrestApps.Subscriptions.Core.Models;
using OrchardCore.Data;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace OrchardCore.CrestApps.Subscriptions.Core.Indexes;

public sealed class SubscriptionsContentItemIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IClock _clock;

    public SubscriptionsContentItemIndexProvider(
        IContentDefinitionManager contentDefinitionManager,
        IClock clock)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _clock = clock;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<SubscriptionsContentItemIndex>()
            .Map(async contentItem =>
            {
                if (!contentItem.TryGet<SubscriptionsPart>(out var part))
                {
                    return null;
                }

                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

                if (definition?.StereotypeEquals(SubscriptionsConstants.Stereotype) == false)
                {
                    return null;
                }

                var createdUtc = contentItem.CreatedUtc ?? _clock.UtcNow;

                return new SubscriptionsContentItemIndex()
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentItemVersionId = contentItem.ContentItemVersionId,
                    ContentType = contentItem.ContentType,
                    Order = part.Sort ?? 0,
                    CreatedUtc = createdUtc,
                    ModifiedUtc = contentItem.ModifiedUtc ?? createdUtc,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
