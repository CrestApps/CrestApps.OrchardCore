using CrestApps.Core;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The variant rules of the product editor (<c>ProductVariantsPartDisplayDriver</c>), shared with the product AI tools:
/// the options are the ones the content type defines, in its order; the variants are generated from the options' values
/// and keep their ids, so stock and past orders keep pointing at them; a variant's prices are zero or more; and no SKU is
/// used by two variants.
/// </summary>
internal static class ProductVariantRules
{
    private static readonly char[] _separators = [',', ';', '\n'];

    /// <summary>
    /// Gets the options the content type defines for its products, in its order. Options a product still has values
    /// for, but the type no longer lists, are dropped on the next save.
    /// </summary>
    /// <param name="typePartDefinition">The variants part of the product's type.</param>
    /// <returns>The option names.</returns>
    public static string[] GetOptionNames(ContentTypePartDefinition typePartDefinition)
    {
        var settings = typePartDefinition?.GetSettings<ProductVariantsPartSettings>();

        return (settings?.Options ?? [])
            .Select(option => option?.Name?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Splits the values typed for an option, separated by commas, semicolons or new lines, without repeats.
    /// </summary>
    /// <param name="values">The values as typed.</param>
    /// <returns>The values.</returns>
    public static List<string> SplitValues(string values)
        => (values ?? string.Empty)
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Builds the options of a product from the type's option names and the values given for each. An option with no
    /// values is left out.
    /// </summary>
    /// <param name="names">The type's option names, in order.</param>
    /// <param name="valuesOf">The values given for an option.</param>
    /// <returns>The options.</returns>
    public static List<ProductOption> BuildOptions(IEnumerable<string> names, Func<string, IEnumerable<string>> valuesOf)
    {
        ArgumentNullException.ThrowIfNull(valuesOf);

        return (names ?? [])
            .Select(name => new ProductOption
            {
                Name = name,
                Values = (valuesOf(name) ?? [])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .Where(option => option.Values.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Sets a product's options and generates its variants from them: a combination that stays keeps its variant, with
    /// its id, SKU and prices; a new one gets a new variant; one gone is dropped.
    /// </summary>
    /// <param name="part">The product's variants part.</param>
    /// <param name="options">The options.</param>
    public static void Apply(ProductVariantsPart part, IList<ProductOption> options)
    {
        ArgumentNullException.ThrowIfNull(part);

        part.Options = options ?? [];
        part.Variants = ProductVariants.Combine(part.Options, part.Variants, UniqueId.GenerateId);
    }

    /// <summary>
    /// Returns whether a variant price is allowed: none, or zero or more.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <returns><see langword="true"/> when the price can be saved.</returns>
    public static bool IsValidPrice(decimal? price)
        => price is not < 0m;

    /// <summary>
    /// Gets the SKUs that more than one variant uses, ignoring case.
    /// </summary>
    /// <param name="variants">The variants.</param>
    /// <returns>The SKUs used twice or more.</returns>
    public static IReadOnlyList<string> FindDuplicateSkus(IEnumerable<ProductVariant> variants)
        => (variants ?? [])
            .Where(variant => !string.IsNullOrEmpty(variant.Sku))
            .GroupBy(variant => variant.Sku, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
}
