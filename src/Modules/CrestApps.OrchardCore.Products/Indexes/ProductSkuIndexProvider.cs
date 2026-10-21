using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

internal sealed class ProductSkuIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<ProductSkuIndex>().Map(CreateIndexes);

    internal static IEnumerable<ProductSkuIndex> CreateIndexes(ContentItem contentItem)
    {
        if ((!contentItem.Published && !contentItem.Latest) || !contentItem.TryGet<ProductPart>(out var product))
        {
            return [];
        }

        var skus = new List<(string Sku, string VariantId)>();

        if (!string.IsNullOrWhiteSpace(product.Sku))
        {
            skus.Add((product.Sku.Trim(), null));
        }

        if (contentItem.TryGet<ProductVariantsPart>(out var variants))
        {
            skus.AddRange(variants.Variants
                .Where(variant => !string.IsNullOrWhiteSpace(variant.Sku))
                .Select(variant => (variant.Sku.Trim(), variant.VariantId)));
        }

        return skus
            .Where(entry => entry.Sku.Length <= ProductSkuIndex.MaxSkuLength)
            .DistinctBy(entry => entry.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ProductSkuIndex
            {
                ContentItemId = contentItem.ContentItemId,
                ContentType = contentItem.ContentType,
                Sku = entry.Sku,
                VariantId = entry.VariantId,
                Published = contentItem.Published,
                Latest = contentItem.Latest,
            })
            .ToArray();
    }
}
