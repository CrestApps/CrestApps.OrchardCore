using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

internal sealed class ProductBundleIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<ProductBundleIndex>().Map(CreateIndexes);

    internal static IEnumerable<ProductBundleIndex> CreateIndexes(ContentItem contentItem)
    {
        if ((!contentItem.Published && !contentItem.Latest) ||
            !contentItem.TryGet<ProductBundlePart>(out var bundle) ||
            bundle.Items is not { Count: > 0 })
        {
            return [];
        }

        return bundle.Items
            .Where(item => !string.IsNullOrEmpty(item?.ContentItemId) && item.ContentItemId.Length <= 26)
            .DistinctBy(item => item.ContentItemId, StringComparer.Ordinal)
            .Select(item => new ProductBundleIndex
            {
                ContentItemId = contentItem.ContentItemId,
                PartContentItemId = item.ContentItemId,
                Quantity = Math.Max(1, item.Quantity),
                Published = contentItem.Published,
                Latest = contentItem.Latest,
            })
            .ToArray();
    }
}
