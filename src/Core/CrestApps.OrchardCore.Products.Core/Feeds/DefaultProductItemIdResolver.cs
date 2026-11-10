using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// Reads feed item ids back through the product SKU index and the content item index. Only a published product that
/// can be bought resolves: a product with variants only through one of its enabled variants, and a product without
/// variants only as itself.
/// </summary>
/// <remarks>
/// Feeds cut an id to <see cref="ProductFeedItemIds.MaxLength"/> characters, so an id of that length also matches the
/// one SKU, or the one variant of the product, it is the start of.
/// </remarks>
public sealed class DefaultProductItemIdResolver : IProductItemIdResolver
{
    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultProductItemIdResolver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentDefinitionManager">The content definition manager, for the product settings of a type.</param>
    public DefaultProductItemIdResolver(
        ISession session,
        IContentDefinitionManager contentDefinitionManager)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc/>
    public async Task<ProductItemReference> ResolveAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        itemId = itemId.Trim();

        var truncated = itemId.Length == ProductFeedItemIds.MaxLength;

        // A SKU comes first: the feed gives an item its SKU whenever it has one.
        var skus = (await _session.QueryIndex<ProductSkuIndex>(index => index.Published && index.Sku == itemId)
            .ListAsync(cancellationToken)).ToList();

        if (skus.Count == 0 && truncated)
        {
            skus = (await _session.QueryIndex<ProductSkuIndex>(index => index.Published && index.Sku.StartsWith(itemId))
                .Take(2)
                .ListAsync(cancellationToken)).ToList();

            if (skus.Count > 1)
            {
                skus.Clear();
            }
        }

        foreach (var sku in skus)
        {
            var product = await GetProductsAsync([sku.ContentItemId], cancellationToken);

            if (product.TryGetValue(sku.ContentItemId, out var contentItem) &&
                await MatchAsync(contentItem, sku.VariantId, variantIdIsPrefix: false) is { } bySku)
            {
                return bySku;
            }
        }

        // Then the product's id, alone or with a variant's after a dash; a content item id can hold dashes itself.
        var candidates = new List<(string ContentItemId, string VariantId)> { (itemId, null) };

        for (var index = itemId.IndexOf('-'); index > 0 && index < itemId.Length - 1; index = itemId.IndexOf('-', index + 1))
        {
            candidates.Add((itemId.Substring(0, index), itemId.Substring(index + 1)));
        }

        var products = await GetProductsAsync(candidates.Select(candidate => candidate.ContentItemId).Distinct(StringComparer.Ordinal), cancellationToken);

        foreach (var (contentItemId, variantId) in candidates)
        {
            if (products.TryGetValue(contentItemId, out var contentItem) &&
                await MatchAsync(contentItem, variantId, variantIdIsPrefix: truncated && variantId is not null) is { } byId)
            {
                return byId;
            }
        }

        return null;
    }

    private async Task<Dictionary<string, ContentItem>> GetProductsAsync(IEnumerable<string> contentItemIds, CancellationToken cancellationToken)
    {
        var ids = contentItemIds.ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId.IsIn(ids) && index.Published)
            .ListAsync(cancellationToken);

        return items
            .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private async Task<ProductItemReference> MatchAsync(ContentItem contentItem, string variantId, bool variantIdIsPrefix)
    {
        if (!contentItem.TryGet<ProductPart>(out var product))
        {
            return null;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        return Match(contentItem, product, ProductAttributeCatalog.GetProductSettings(definition), variantId, variantIdIsPrefix);
    }

    /// <summary>
    /// Returns the reference to a product, or to the variant of it the id names, when it can be bought; otherwise
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="contentItem">The published product.</param>
    /// <param name="product">The product's product part.</param>
    /// <param name="settings">The product settings of its content type, or <see langword="null"/>.</param>
    /// <param name="variantId">The variant's id, or <see langword="null"/> for the product itself.</param>
    /// <param name="variantIdIsPrefix">Whether <paramref name="variantId"/> may be the start of the variant's id, as in
    /// an id a feed cut short.</param>
    public static ProductItemReference Match(ContentItem contentItem, ProductPart product, ProductPartSettings settings, string variantId, bool variantIdIsPrefix)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(product);

        if (!product.CanBePurchased(settings))
        {
            return null;
        }

        var variants = contentItem.TryGet<ProductVariantsPart>(out var variantsPart)
            ? variantsPart.GetAvailable().ToList()
            : [];

        if (variants.Count == 0)
        {
            return string.IsNullOrEmpty(variantId)
                ? new ProductItemReference
                {
                    ItemId = ProductFeedItemIds.Create(contentItem.ContentItemId, null, product.Sku),
                    ContentItemId = contentItem.ContentItemId,
                    Sku = string.IsNullOrEmpty(product.Sku) ? null : product.Sku,
                }
                : null;
        }

        if (string.IsNullOrEmpty(variantId))
        {
            return null;
        }

        var variant = variants.FirstOrDefault(candidate => string.Equals(candidate.VariantId, variantId, StringComparison.Ordinal));

        if (variant is null && variantIdIsPrefix)
        {
            var matches = variants.Where(candidate => candidate.VariantId?.StartsWith(variantId, StringComparison.Ordinal) == true).Take(2).ToList();

            variant = matches.Count == 1 ? matches[0] : null;
        }

        return variant is null
            ? null
            : new ProductItemReference
            {
                ItemId = ProductFeedItemIds.Create(contentItem.ContentItemId, variant.VariantId, variant.Sku),
                ContentItemId = contentItem.ContentItemId,
                VariantId = variant.VariantId,
                Sku = string.IsNullOrEmpty(variant.Sku) ? null : variant.Sku,
            };
    }
}
