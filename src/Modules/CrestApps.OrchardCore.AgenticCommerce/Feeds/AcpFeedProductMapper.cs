using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Turns product feed items into the Agentic Commerce Protocol feed API's <c>Product</c> objects (ACP 2026-04-17): one
/// product per store product, with each item as one of its variants. A variant's id is the item id checkouts accept.
/// </summary>
public static class AcpFeedProductMapper
{
    /// <summary>
    /// Returns the ACP products of the items, in the order their first item comes in.
    /// </summary>
    /// <param name="items">The product feed items.</param>
    /// <param name="utcNow">The time now, which says whether a sale has started.</param>
    public static IReadOnlyList<JsonObject> ToProducts(IEnumerable<ProductFeedItem> items, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Where(item => !string.IsNullOrEmpty(item.Id))
            .GroupBy(item => item.ContentItemId ?? item.ItemGroupId ?? item.Id, StringComparer.Ordinal)
            .Select(group => ToProduct(group.Key, [.. group], utcNow))
            .ToList();
    }

    private static JsonObject ToProduct(string id, IReadOnlyList<ProductFeedItem> items, DateTime utcNow)
    {
        var first = items[0];
        var product = new JsonObject
        {
            ["id"] = id,
            ["title"] = first.ProductTitle ?? first.Title,
        };

        Add(product, "description", Description(first.Description));
        Add(product, "url", first.Link);
        Add(product, "media", Media(first.ImageLink, first.AdditionalImageLinks));

        product["variants"] = new JsonArray([.. items.Select(item => (JsonNode)ToVariant(item, utcNow))]);

        return product;
    }

    private static JsonObject ToVariant(ProductFeedItem item, DateTime utcNow)
    {
        var variant = new JsonObject
        {
            ["id"] = item.Id,
            ["title"] = item.Title,
        };

        Add(variant, "description", Description(item.Description));
        Add(variant, "url", item.Link);

        if (!string.IsNullOrWhiteSpace(item.Gtin))
        {
            variant["barcodes"] = new JsonArray(new JsonObject
            {
                ["type"] = "gtin",
                ["value"] = item.Gtin.Trim(),
            });
        }

        if (!string.IsNullOrEmpty(item.Currency))
        {
            // A sale that is on is the price, with the regular price as the list price; one to come isn't yet.
            var onSale = item.SalePrice is not null && (item.SaleStartsUtc is null || item.SaleStartsUtc <= utcNow) && (item.SaleEndsUtc is null || item.SaleEndsUtc > utcNow);

            variant["price"] = Price(onSale ? item.SalePrice.Value : item.Price, item.Currency);

            if (onSale)
            {
                variant["list_price"] = Price(item.Price, item.Currency);
            }
        }

        variant["availability"] = new JsonObject
        {
            ["available"] = item.Availability is "in_stock" or "backorder" or "preorder",
            ["status"] = item.Availability switch
            {
                "in_stock" => "in_stock",
                "backorder" => "backorder",
                "preorder" => "preorder",
                _ => "out_of_stock",
            },
        };

        var category = item.ProductType ?? item.GoogleProductCategory;

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryNode = new JsonObject { ["value"] = category };

            if (item.ProductType is null)
            {
                categoryNode["taxonomy"] = "google_product_category";
            }

            variant["categories"] = new JsonArray(categoryNode);
        }

        if (!string.IsNullOrWhiteSpace(item.Condition))
        {
            variant["condition"] = new JsonArray(item.Condition);
        }

        if (item.VariantOptions is { Count: > 0 } options)
        {
            variant["variant_options"] = new JsonArray([.. options
                .Where(option => !string.IsNullOrWhiteSpace(option.Value))
                .Select(option => (JsonNode)new JsonObject
                {
                    ["name"] = option.Key,
                    ["value"] = option.Value,
                })]);
        }

        var media = Media(item.ImageLink, []);

        if (media is not null)
        {
            variant["media"] = media;
        }

        var seller = new JsonObject();

        Add(seller, "name", item.SellerName);

        var links = new JsonArray();

        AddLink(links, "terms_of_use", item.TermsOfServiceUrl);
        AddLink(links, "privacy_policy", item.PrivacyPolicyUrl);
        AddLink(links, "return_policy", item.ReturnPolicyUrl);

        if (links.Count > 0)
        {
            seller["links"] = links;
        }

        if (seller.Count > 0)
        {
            variant["seller"] = seller;
        }

        return variant;
    }

    /// <summary>
    /// Returns an amount as ACP's feed API prices it: whole minor units and the uppercase currency code.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO 4217 code of its currency.</param>
    public static JsonObject Price(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        return new JsonObject
        {
            ["amount"] = Math.Max(0, CurrencyScale.ToMinorUnits(amount, currency)),
            ["currency"] = currency.ToUpperInvariant(),
        };
    }

    private static JsonObject Description(string text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : new JsonObject { ["plain"] = text };

    private static JsonArray Media(string image, IEnumerable<string> more)
    {
        var urls = new[] { image }
            .Concat(more ?? [])
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return urls.Count == 0
            ? null
            : new JsonArray([.. urls.Select(url => (JsonNode)new JsonObject
            {
                ["type"] = "image",
                ["url"] = url,
            })]);
    }

    private static void AddLink(JsonArray links, string type, string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            links.Add(new JsonObject
            {
                ["type"] = type,
                ["url"] = url,
            });
        }
    }

    private static void Add(JsonObject target, string name, JsonNode value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }

    private static void Add(JsonObject target, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[name] = value;
        }
    }
}
