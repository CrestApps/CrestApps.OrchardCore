using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Reads the add-ons a buyer chose for a product: keeps only the product's own, trims what was typed, checks the
/// required ones, and works out what they add to the price.
/// </summary>
public static class ProductAddOns
{
    /// <summary>
    /// The value a ticked box is kept as.
    /// </summary>
    public const string Checked = "true";

    /// <summary>
    /// Returns the add-ons chosen, the product's own only, in its order: a text that was typed, a box that was
    /// ticked. Nothing chosen gives <see langword="null"/>.
    /// </summary>
    public static Dictionary<string, string> Normalize(ProductPart product, IReadOnlyDictionary<string, string> chosen)
    {
        if (product?.AddOns is not { Count: > 0 } offered || chosen is not { Count: > 0 })
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var addOn in offered)
        {
            if (string.IsNullOrEmpty(addOn.AddOnId) || !chosen.TryGetValue(addOn.AddOnId, out var value))
            {
                continue;
            }

            if (addOn.Kind == ProductAddOnKind.Checkbox)
            {
                if (value is "true" or "on" or "True")
                {
                    result[addOn.AddOnId] = Checked;
                }
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                var text = value.Trim();
                result[addOn.AddOnId] = text.Length > ProductAddOn.MaxTextLength ? text[..ProductAddOn.MaxTextLength] : text;
            }
        }

        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// Returns the first required add-on the buyer left out, or <see langword="null"/>.
    /// </summary>
    public static ProductAddOn FindMissing(ProductPart product, IReadOnlyDictionary<string, string> chosen)
        => product?.AddOns?.FirstOrDefault(addOn => addOn.Required && !string.IsNullOrEmpty(addOn.AddOnId) && (chosen is null || !chosen.ContainsKey(addOn.AddOnId)));

    /// <summary>
    /// Returns what the chosen add-ons add to each unit.
    /// </summary>
    public static decimal GetSurcharge(ProductPart product, IReadOnlyDictionary<string, string> chosen)
        => chosen is not { Count: > 0 } || product?.AddOns is null
            ? 0m
            : product.AddOns.Where(addOn => !string.IsNullOrEmpty(addOn.AddOnId) && chosen.ContainsKey(addOn.AddOnId)).Sum(addOn => Math.Max(0m, addOn.Price));

    /// <summary>
    /// Returns the chosen add-ons as they read on a line: label, what was chosen (the text, or nothing for a box) and
    /// the extra price.
    /// </summary>
    public static IReadOnlyList<ProductAddOnChoice> Describe(ProductPart product, IReadOnlyDictionary<string, string> chosen)
        => chosen is not { Count: > 0 } || product?.AddOns is null
            ? []
            : [.. product.AddOns
                .Where(addOn => !string.IsNullOrEmpty(addOn.AddOnId) && chosen.ContainsKey(addOn.AddOnId))
                .Select(addOn => new ProductAddOnChoice(addOn.AddOnId, addOn.Label, addOn.Kind == ProductAddOnKind.Checkbox ? null : chosen[addOn.AddOnId], Math.Max(0m, addOn.Price)))];

    /// <summary>
    /// Returns whether two choices of add-ons are the same, so the same product with them belongs on one line.
    /// </summary>
    public static bool AreSame(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if ((left?.Count ?? 0) != (right?.Count ?? 0))
        {
            return false;
        }

        return left is null || left.All(pair => right.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));
    }
}
