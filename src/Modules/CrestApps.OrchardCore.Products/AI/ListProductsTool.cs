using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Lists products, the content items whose type has the Product part, as Commerce › Products does: the latest version
/// of each, for a caller with <c>ListContent</c>, and only of the product types the caller may view
/// (<c>ViewContent</c>, checked per type as the Catalog API does). Units available come from the stock providers
/// (<see cref="IProductAvailabilityProvider"/>) and are left out when nothing counts stock.
/// </summary>
internal sealed class ListProductsTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listProducts";

    // A sale is worked out per product, so the on-sale filter reads at most this many products.
    private const int MaxOnSaleScan = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Text to find in the product's title."
        },
        "sku": {
          "type": "string",
          "description": "The exact SKU of a product or of one of its variants, as stored."
        },
        "contentType": {
          "type": "string",
          "description": "Only products of this content type, such as Product."
        },
        "onSale": {
          "type": "boolean",
          "description": "true for products on sale right now (a sale price whose dates include now), false for those that are not."
        },
        "status": {
          "type": "string",
          "enum": ["any", "published", "draft"],
          "description": "published for products whose latest version is published, draft for those never published or with unpublished changes. any when left out."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Products per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists products (content items whose type has the Product part) with content item id, type, SKU, price and currency, sale price and dates, variant count, published state and, when Inventory is on, units available. Filters by text, exact SKU of a product or variant, content type, on sale, and published or draft, with paging; use it to find a product before editing it, its price or its stock.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CommonPermissions.ListContent;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var definitions = services.GetRequiredService<IContentDefinitionManager>();
        var productTypes = await services.GetRequiredService<ProductContentTypes>().GetProductTypesAsync(definitions);
        var types = new List<string>();

        foreach (var type in productTypes)
        {
            // Checked once per type, as the Catalog API does: a caller never sees a type they may not view.
            if (await IsAuthorizedAsync(services, caller, CommonPermissions.ViewContent, new ContentItem { ContentType = type }))
            {
                types.Add(type);
            }
        }

        var contentType = ReadString(arguments, "contentType");

        if (contentType is not null)
        {
            if (!types.Contains(contentType, StringComparer.Ordinal))
            {
                return $"Nothing was listed: '{contentType}' is not a product type you may view. The product types are: {(types.Count == 0 ? "none" : string.Join(", ", types))}.";
            }

            types = [contentType];
        }

        var (page, pageSize) = ReadPaging(arguments);

        if (types.Count == 0)
        {
            return Json(new { items = Array.Empty<object>(), total = 0, page, pageSize, truncated = false });
        }

        var session = services.GetRequiredService<YesSqlSession>();
        var sku = ReadString(arguments, "sku");
        var matchedVariants = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] skuIds = null;

        if (sku is not null)
        {
            var matches = await session.QueryIndex<ProductSkuIndex>(index => index.Sku == sku && index.Latest).ListAsync(cancellationToken);

            foreach (var match in matches)
            {
                matchedVariants.TryAdd(match.ContentItemId, match.VariantId);
            }

            skuIds = [.. matchedVariants.Keys];

            if (skuIds.Length == 0)
            {
                return Json(new { items = Array.Empty<object>(), total = 0, page, pageSize, truncated = false, note = $"No product or variant has the SKU '{sku}'." });
            }
        }

        var query = session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && index.Latest);
        var text = ReadString(arguments, "query");

        if (text is not null)
        {
            query = query.Where(index => index.DisplayText.Contains(text));
        }

        if (skuIds is not null)
        {
            query = query.Where(index => index.ContentItemId.IsIn(skuIds));
        }

        switch (ReadString(arguments, "status")?.ToLowerInvariant())
        {
            case "published":
                query = query.Where(index => index.Published);
                break;
            case "draft":
                query = query.Where(index => !index.Published);
                break;
        }

        var now = services.GetRequiredService<IClock>().UtcNow;
        var onSale = ReadBool(arguments, "onSale");
        IReadOnlyList<ContentItem> items;
        int total;
        var scanLimited = false;

        if (onSale is null)
        {
            total = await query.CountAsync(cancellationToken);
            items = [.. await query.OrderByDescending(index => index.ModifiedUtc).Skip((page - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken)];
        }
        else
        {
            var scanned = (await query.OrderByDescending(index => index.ModifiedUtc).Take(MaxOnSaleScan + 1).ListAsync(cancellationToken)).ToList();

            scanLimited = scanned.Count > MaxOnSaleScan;

            var matching = scanned.Take(MaxOnSaleScan).Where(item => IsOnSale(item, now) == onSale.Value).ToList();

            total = matching.Count;
            items = [.. matching.Skip((page - 1) * pageSize).Take(pageSize)];
        }

        var ids = items.Where(item => !item.Published).Select(item => item.ContentItemId).ToArray();
        var publishedIds = ids.Length == 0
            ? []
            : (await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(ids) && index.Published).ListAsync(cancellationToken)).Select(index => index.ContentItemId).ToHashSet(StringComparer.Ordinal);

        var availability = await GetAvailabilityAsync(services, items, cancellationToken);
        var rows = new List<object>();

        foreach (var item in items)
        {
            var product = GetPart<ProductPart>(item);
            var variants = GetPart<ProductVariantsPart>(item)?.Variants ?? [];
            var type = await definitions.GetTypeDefinitionAsync(item.ContentType);

            rows.Add(new
            {
                contentItemId = item.ContentItemId,
                title = item.DisplayText,
                contentType = item.ContentType,
                sku = product?.Sku,
                matchedVariantId = matchedVariants.GetValueOrDefault(item.ContentItemId),
                price = product?.Price,
                currency = GetCurrency(product, type),
                compareAtPrice = product?.CompareAtPrice,
                salePrice = product?.SalePrice,
                saleStarts = await LocalTimeAsync(services, product?.SaleStartsUtc),
                saleEnds = await LocalTimeAsync(services, product?.SaleEndsUtc),
                onSaleNow = IsOnSale(item, now),
                variants = variants.Count,
                status = item.Published ? "published" : publishedIds.Contains(item.ContentItemId) ? "published, with unpublished changes" : "draft",
                unitsAvailable = availability.TryGetValue(item.ContentItemId, out var units) ? units.Available : (int?)null,
                allowBackorder = availability.TryGetValue(item.ContentItemId, out var backorder) ? backorder.AllowBackorder : (bool?)null,
                modified = await LocalTimeAsync(services, item.ModifiedUtc),
            });
        }

        return Json(new
        {
            items = rows,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
            note = scanLimited ? $"The on-sale filter read the {MaxOnSaleScan} most recently changed products only; narrow the list with query, sku or contentType." : null,
        });
    }

    // On sale now: the product's sale price, or one of its variants', applies at this moment.
    private static bool IsOnSale(ContentItem item, DateTime utcNow)
    {
        var product = GetPart<ProductPart>(item);

        if (product is null)
        {
            return false;
        }

        var variants = GetPart<ProductVariantsPart>(item)?.Variants ?? [];

        return ProductSale.GetSalePrice(product, null, utcNow) is not null ||
            variants.Any(variant => variant.Enabled && ProductSale.GetSalePrice(product, variant, utcNow) is not null);
    }

    private static async Task<Dictionary<string, ProductAvailability>> GetAvailabilityAsync(IServiceProvider services, IReadOnlyList<ContentItem> items, CancellationToken cancellationToken)
    {
        var answer = new Dictionary<string, ProductAvailability>(StringComparer.Ordinal);
        var providers = services.GetServices<IProductAvailabilityProvider>().ToArray();

        if (providers.Length == 0 || items.Count == 0)
        {
            return answer;
        }

        var keys = new List<ProductStockKey>();

        foreach (var item in items)
        {
            var variants = GetPart<ProductVariantsPart>(item)?.Variants ?? [];

            if (variants.Count == 0)
            {
                keys.Add(new ProductStockKey(item.ContentItemId, null));
            }
            else
            {
                keys.AddRange(variants.Select(variant => new ProductStockKey(item.ContentItemId, variant.VariantId)));
            }
        }

        var found = await providers.GetAvailabilityAsync(keys, cancellationToken: cancellationToken);

        foreach (var group in found.GroupBy(entry => entry.Key.ContentItemId, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            var combined = entries.Length == 1 && entries[0].Key.VariantId is null
                ? entries[0].Value
                : ProductAvailabilityProviderExtensions.Combine(entries.Select(entry => entry.Value));

            if (combined is not null)
            {
                answer[group.Key] = combined;
            }
        }

        return answer;
    }
}
