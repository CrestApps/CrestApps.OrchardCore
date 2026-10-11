using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Data;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

/// <summary>
/// Maps subscription content items to index rows used for listing, ordering, and version-state filtering.
/// </summary>
public sealed class SubscriptionsContentItemIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsContentItemIndexProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to identify subscription content types.</param>
    /// <param name="clock">The clock used to provide a fallback creation time for content items without a creation date.</param>
    public SubscriptionsContentItemIndexProvider(
        IContentDefinitionManager contentDefinitionManager,
        IClock clock)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _clock = clock;
    }

    /// <summary>
    /// Describes how subscription content items are projected into <see cref="SubscriptionsContentItemIndex"/> rows.
    /// </summary>
    /// <param name="context">The YesSql describe context for content items.</param>
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<SubscriptionsContentItemIndex>()
            .Map(async contentItem =>
            {
                if (!contentItem.TryGet<SubscriptionPart>(out var part))
                {
                    return null;
                }

                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

                if (definition?.StereotypeEquals(SubscriptionConstants.Stereotype) == false)
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
