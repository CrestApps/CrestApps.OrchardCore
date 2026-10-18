using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// How much a product weighs, and the shipping class it is in. A variant can weigh more or less than its product.
/// </summary>
public sealed class ShippingPart : ContentPart
{
    /// <summary>
    /// Gets or sets the weight of one unit in the store's weight unit, or <see langword="null"/> when it isn't known (it
    /// then counts as nothing).
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Gets or sets the product's shipping class, such as <em>Bulky</em>, or <see langword="null"/> for none.
    /// </summary>
    public string ShippingClass { get; set; }

    /// <summary>
    /// Gets or sets the weight of variants that differ from the product's, by variant id.
    /// </summary>
    public Dictionary<string, decimal> VariantWeights { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the weight of one unit of a variant, or of the product when the variant has none of its own.
    /// </summary>
    public decimal GetWeight(string variantId)
        => !string.IsNullOrEmpty(variantId) && VariantWeights?.TryGetValue(variantId, out var weight) == true
        ? weight
        : Weight ?? 0;
}
