using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// Reads the values of a product the products data sets share.
/// </summary>
internal static class ProductReportValues
{
    private const int MaxCategories = 100;

    /// <summary>
    /// Gets a part of a product.
    /// </summary>
    /// <typeparam name="TPart">The type of the part.</typeparam>
    /// <param name="product">The product, or <see langword="null"/>.</param>
    /// <returns>The part, or <see langword="null"/> when the product has none.</returns>
    public static TPart Part<TPart>(ContentItem product)
        where TPart : ContentPart
    {
        return product is not null && product.TryGet<TPart>(out var part) ? part : null;
    }

    /// <summary>
    /// Gets the currency a product's prices are in: its own, else its content type's default, in upper case, as the
    /// checkout reads it.
    /// </summary>
    /// <param name="product">The product part, or <see langword="null"/>.</param>
    /// <param name="settings">The content type's product settings, or <see langword="null"/>.</param>
    /// <returns>The currency code, or <see langword="null"/> when the product has none.</returns>
    public static string Currency(ProductPart product, ProductPartSettings settings)
    {
        var currency = string.IsNullOrEmpty(product?.Currency)
            ? settings?.DefaultCurrency
            : product.Currency;

        return string.IsNullOrWhiteSpace(currency)
            ? null
            : currency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Gets the categories a product is in, the taxonomy terms its taxonomy fields hold, as a comma-separated list of
    /// their content item identifiers.
    /// </summary>
    /// <param name="contentItem">The product.</param>
    /// <returns>The list, or <see langword="null"/> when the product is in no category.</returns>
    public static string CategoryIds(ContentItem contentItem)
    {
        if (contentItem is null)
        {
            return null;
        }

        var ids = new List<string>();

        Collect((JsonObject)contentItem.Content, ids);

        return Join(ids.Distinct(StringComparer.Ordinal).Take(MaxCategories));
    }

    /// <summary>
    /// Joins values into a comma-separated list, leaving out the empty ones.
    /// </summary>
    /// <param name="values">The values, or <see langword="null"/>.</param>
    /// <returns>The list, or <see langword="null"/> when no value is left.</returns>
    public static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }

    private static void Collect(JsonNode node, List<string> ids)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, value) in jsonObject)
                {
                    if (name == "TermContentItemIds" && value is JsonArray terms)
                    {
                        ids.AddRange(terms
                            .Where(term => term?.GetValueKind() == JsonValueKind.String)
                            .Select(term => term.GetValue<string>())
                            .Where(id => !string.IsNullOrEmpty(id)));
                    }
                    else
                    {
                        Collect(value, ids);
                    }
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    Collect(item, ids);
                }

                break;
        }
    }
}
