using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Finds products for UCP agents and writes them as UCP products, each with its variants, from the same items the
/// product feeds publish: the same ids, prices, photos and availability.
/// </summary>
public sealed class UcpCatalog
{
    private const int MaxResults = 50;

    private readonly IProductSearchService _search;
    private readonly IProductFeedService _feeds;
    private readonly IContentDefinitionManager _definitions;

    public UcpCatalog(
        IProductSearchService search,
        IProductFeedService feeds,
        IContentDefinitionManager definitions)
    {
        _search = search;
        _feeds = feeds;
        _definitions = definitions;
    }

    /// <summary>
    /// Returns the products matching a query, a page at a time.
    /// </summary>
    /// <param name="query">The words to look for, or <see langword="null"/> for every product.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="currency">The currency, or <see langword="null"/> for the store's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<(JsonArray Products, int Total)> SearchAsync(string query, int skip, int take, string currency, CancellationToken cancellationToken = default)
    {
        var types = (await _definitions.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == "ProductPart"))
            .Select(type => type.Name)
            .ToList();

        var result = await _search.SearchAsync(new ProductSearchQuery
        {
            ContentTypes = types,
            Keyword = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            Skip = Math.Max(0, skip),
            Take = Math.Clamp(take, 1, MaxResults),
        }, cancellationToken);

        return (await WriteAsync(result.ContentItemIds, currency, cancellationToken), result.Total);
    }

    /// <summary>
    /// Returns the products of content items, in their order.
    /// </summary>
    /// <param name="contentItemIds">The products' content item ids.</param>
    /// <param name="currency">The currency, or <see langword="null"/> for the store's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonArray> WriteAsync(IEnumerable<string> contentItemIds, string currency, CancellationToken cancellationToken = default)
    {
        var ids = (contentItemIds ?? []).Distinct(StringComparer.Ordinal).ToList();
        var products = new JsonArray();

        if (ids.Count == 0)
        {
            return products;
        }

        var feed = await _feeds.BuildAsync(ids, currency, cancellationToken);

        foreach (var group in feed.Items.Where(item => item.ShowToAgents).GroupBy(item => item.ContentItemId).OrderBy(group => ids.IndexOf(group.Key)))
        {
            var first = group.First();
            var product = new JsonObject
            {
                ["id"] = group.Key,
                ["title"] = first.ProductTitle ?? first.Title,
                ["url"] = first.Link,
            };

            if (!string.IsNullOrEmpty(first.Description))
            {
                product["description"] = new JsonObject { ["plain"] = first.Description };
            }

            AgentJson.AddIf(product, "brand", first.Brand);

            var media = new JsonArray();

            foreach (var image in new[] { first.ImageLink }.Concat(first.AdditionalImageLinks ?? []).Where(url => !string.IsNullOrEmpty(url)).Distinct())
            {
                media.Add(new JsonObject { ["type"] = "image", ["url"] = image });
            }

            product["media"] = media;

            var variants = new JsonArray();

            foreach (var item in group)
            {
                var price = item.SalePrice is { } sale && sale < item.Price ? sale : item.Price;
                var variant = new JsonObject
                {
                    ["id"] = item.Id,
                    ["title"] = item.Title,
                    ["price"] = new JsonObject { ["amount"] = AgentJson.ToMinor(price, item.Currency), ["currency"] = item.Currency?.ToUpperInvariant() },
                    ["availability"] = new JsonObject
                    {
                        ["available"] = item.Availability is "in_stock" or "backorder" or "preorder",
                        ["status"] = item.Availability,
                    },
                };

                if (price < item.Price)
                {
                    variant["list_price"] = new JsonObject { ["amount"] = AgentJson.ToMinor(item.Price, item.Currency), ["currency"] = item.Currency?.ToUpperInvariant() };
                }

                if (item.VariantOptions?.Count > 0)
                {
                    variant["options"] = new JsonArray([.. item.VariantOptions.Select(option => (JsonNode)new JsonObject { ["name"] = option.Key, ["value"] = option.Value })]);
                }

                if (!string.IsNullOrEmpty(item.Gtin))
                {
                    variant["barcodes"] = new JsonArray(new JsonObject { ["type"] = "gtin", ["value"] = item.Gtin });
                }

                if (item.AvailableQuantity is { } quantity)
                {
                    variant["available_quantity"] = quantity;
                }

                variants.Add(variant);
            }

            product["variants"] = variants;
            products.Add(product);
        }

        return products;
    }
}
