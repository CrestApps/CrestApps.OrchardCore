using OrchardCore.Modules;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services.Attributes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Adds a product's variants to its search record: the option values of the variants on sale, so a filter on Size
/// finds the product when any of them comes in that size; their SKUs, for keyword search; and the price range, so a
/// card shows "From" the lowest price and price filters and sorts use it.
/// </summary>
public sealed class ProductVariantsSearchDocumentContributor : IProductSearchDocumentContributor
{
    /// <summary>
    /// The key of the variants' SKUs, which keyword search matches.
    /// </summary>
    public const string SkuKey = "Variant.Sku";

    private readonly IClock _clock;

    public ProductVariantsSearchDocumentContributor()
    {
    }

    public ProductVariantsSearchDocumentContributor(IClock clock)
    {
        _clock = clock;
    }

    public Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(document);

        if (!contentItem.TryGet<ProductVariantsPart>(out var part) || !part.HasVariants)
        {
            return Task.CompletedTask;
        }

        var available = part.GetAvailable().ToList();

        foreach (var option in part.Options ?? [])
        {
            if (string.IsNullOrEmpty(option?.Name))
            {
                continue;
            }

            var values = option.Values ?? [];

            foreach (var value in available
                .Select(variant => variant.Options.TryGetValue(option.Name, out var chosen) ? chosen : null)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var order = values.ToList().FindIndex(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));

                document.Attributes.Add(new ProductAttributeValue
                {
                    Key = ProductVariants.AttributeKey(option.Name),
                    Text = value,
                    Label = value,
                    Order = order < 0 ? values.Count : order,
                    Searchable = true,
                });
            }
        }

        foreach (var sku in available.Select(variant => variant.Sku).Where(sku => !string.IsNullOrEmpty(sku)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            document.Attributes.Add(new ProductAttributeValue { Key = SkuKey, Text = sku, Searchable = true });
        }

        document.ImagePath ??= available.Select(variant => variant.ImagePath).FirstOrDefault(path => !string.IsNullOrEmpty(path));

        // A price that is not shown stays out of the record (see ProductSearchDocumentBuilder).
        if (document.PriceDisplay != ProductPriceDisplay.Show)
        {
            return Task.CompletedTask;
        }

        contentItem.TryGet<ProductPart>(out var product);
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var prices = available.Select(variant => ProductSale.GetPrice(product, variant, now)).Where(price => price.HasValue).Select(price => price.Value).ToList();

        if (prices.Count == 0)
        {
            return Task.CompletedTask;
        }

        var min = prices.Min();
        var max = prices.Max();

        document.Price = min;
        document.MaxPrice = max > min ? max : null;

        // A compare-at price is shown only when every variant shares it.
        var compareAt = available.Select(variant => ProductSale.GetCompareAtPrice(product, variant, now)).Distinct().ToList();
        document.CompareAtPrice = compareAt.Count == 1 && document.MaxPrice is null ? compareAt[0] : null;

        // One price row per variant price, so a price filter finds the product when any variant is in range.
        document.Attributes.RemoveAll(value => value.Key == ProductAttributeKeys.Price);

        foreach (var price in prices.Distinct())
        {
            document.Attributes.Add(new ProductAttributeValue
            {
                Key = ProductAttributeKeys.Price,
                Text = FieldValueReader.ToInvariant(price),
                Number = price,
            });
        }

        return Task.CompletedTask;
    }
}
