using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The versions a product is sold in: its options (Size, Color) and one variant per combination of their values,
/// each with its own SKU, price, compare-at price, image and an on/off switch.
/// </summary>
/// <remarks>
/// The option names come from the content type (<see cref="ProductVariantsPartSettings"/>), so every product of the
/// type is filtered the same way; each product picks its own values. A buyer chooses a variant, and its SKU and
/// price are what the cart and order record.
/// </remarks>
public sealed class ProductVariantsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the values the product offers for each option.
    /// </summary>
    public IList<ProductOption> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets one variant per combination of option values.
    /// </summary>
    public IList<ProductVariant> Variants { get; set; } = [];

    /// <summary>
    /// The variants a buyer can choose.
    /// </summary>
    public IEnumerable<ProductVariant> GetAvailable()
        => Variants?.Where(variant => variant.Enabled) ?? [];

    public bool HasVariants
        => Variants?.Count > 0;

    public ProductVariant Find(string variantId)
        => string.IsNullOrEmpty(variantId)
            ? null
            : Variants?.FirstOrDefault(variant => string.Equals(variant.VariantId, variantId, StringComparison.Ordinal));
}

/// <summary>
/// The values a product offers for one option, such as S, M and L for Size.
/// </summary>
public sealed class ProductOption
{
    public string Name { get; set; }

    public IList<string> Values { get; set; } = [];
}

/// <summary>
/// One version of a product: a value for each option, with what is sold at that combination.
/// </summary>
public sealed class ProductVariant
{
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the value of each option, by option name.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the variant's SKU. The product's SKU is used when it is empty.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the variant's price. The product's price is used when it is empty.
    /// </summary>
    public decimal? Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    /// <summary>
    /// Gets or sets the variant's sale price, during the product's sale. A variant without one is on sale at the
    /// product's sale price when it sells at the product's price, and not on sale when it has a price of its own.
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Gets or sets the path of the variant's image in the media library.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets whether buyers can choose the variant.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The variant's values in option order, such as "Red / M".
    /// </summary>
    public string GetTitle(IEnumerable<string> optionNames)
        => string.Join(" / ", (optionNames ?? Options.Keys)
            .Select(name => Options.TryGetValue(name, out var value) ? value : null)
            .Where(value => !string.IsNullOrEmpty(value)));
}

/// <summary>
/// The options of a product type's variants.
/// </summary>
public sealed class ProductVariantsPartSettings
{
    public IList<ProductVariantOptionSettings> Options { get; set; } = [];
}

/// <summary>
/// One option of a product type, such as Size.
/// </summary>
public sealed class ProductVariantOptionSettings
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether listings can offer a filter on the option.
    /// </summary>
    public bool Filterable { get; set; }
}

/// <summary>
/// Works out a product's variants from its options.
/// </summary>
public static class ProductVariants
{
    /// <summary>
    /// The attribute key of an option, for filters and the search index.
    /// </summary>
    public static string AttributeKey(string optionName)
        => $"Option.{optionName}";

    /// <summary>
    /// One variant per combination of option values. A combination that already has a variant keeps it, with its
    /// SKU, prices and switch; a variant whose combination is gone is dropped.
    /// </summary>
    /// <remarks>
    /// When the type gains an option, a variant made before it carries on as the first combination that agrees with
    /// its values, so a size M keeps its SKU and price as M / Red. When the type loses an option, the first variant
    /// that agrees with a combination carries on as it. Exact matches are settled first, so they are never taken.
    /// </remarks>
    public static List<ProductVariant> Combine(IEnumerable<ProductOption> options, IEnumerable<ProductVariant> existing, Func<string> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);

        var used = (options ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option?.Name) && option.Values?.Any(value => !string.IsNullOrWhiteSpace(value)) == true)
            .Select(option => new ProductOption
            {
                Name = option.Name.Trim(),
                Values = option.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            })
            .ToList();

        if (used.Count == 0)
        {
            return [];
        }

        IEnumerable<Dictionary<string, string>> combinations = [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)];

        foreach (var option in used)
        {
            combinations = combinations.SelectMany(combination => option.Values.Select(value =>
                new Dictionary<string, string>(combination, StringComparer.OrdinalIgnoreCase) { [option.Name] = value }));
        }

        var previous = (existing ?? []).Where(variant => variant?.Options is not null).ToList();
        var wanted = combinations.ToList();
        var matches = new ProductVariant[wanted.Count];

        // Exact matches first, then a variant that agrees with a combination on the options both have.
        foreach (var exact in new[] { true, false })
        {
            for (var i = 0; i < wanted.Count; i++)
            {
                if (matches[i] is not null)
                {
                    continue;
                }

                var match = previous.FirstOrDefault(variant => exact
                    ? SameCombination(variant.Options, wanted[i])
                    : Agrees(variant.Options, wanted[i]));

                if (match is not null)
                {
                    matches[i] = match;
                    previous.Remove(match);
                }
            }
        }

        var variants = new List<ProductVariant>(wanted.Count);

        for (var i = 0; i < wanted.Count; i++)
        {
            var variant = matches[i] ?? new ProductVariant { VariantId = newId() };
            variant.Options = wanted[i];
            variants.Add(variant);
        }

        return variants;
    }

    private static bool SameCombination(Dictionary<string, string> left, Dictionary<string, string> right)
        => left.Count == right.Count &&
           right.All(pair => left.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));

    // One combination's options are all among the other's, with the same values: an option was added or removed.
    private static bool Agrees(Dictionary<string, string> left, Dictionary<string, string> right)
    {
        var (smaller, larger) = left.Count <= right.Count ? (left, right) : (right, left);

        return smaller.Count > 0 &&
            smaller.All(pair => larger.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));
    }
}
