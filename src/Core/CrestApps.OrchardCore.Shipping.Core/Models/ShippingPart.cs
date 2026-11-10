using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// How much a product weighs, how big it is, and the shipping class it is in. A variant can weigh more or less, and be
/// bigger or smaller, than its product.
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
    /// Gets or sets the length of one unit, packed, in centimeters for a store weighing in kilograms and inches
    /// otherwise, or <see langword="null"/> when it isn't known.
    /// </summary>
    public decimal? Length { get; set; }

    /// <summary>
    /// Gets or sets the width of one unit, packed, in the same unit as <see cref="Length"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Width { get; set; }

    /// <summary>
    /// Gets or sets the height of one unit, packed, in the same unit as <see cref="Length"/>, or <see langword="null"/>.
    /// </summary>
    public decimal? Height { get; set; }

    /// <summary>
    /// Gets or sets the size of variants that differ from the product's, by variant id.
    /// </summary>
    public Dictionary<string, ShippingDimensions> VariantDimensions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets whether the weight of a variant, or of the product when <paramref name="variantId"/> is empty, is known.
    /// </summary>
    /// <param name="variantId">The variant's id, or <see langword="null"/> for the product.</param>
    public bool HasWeight(string variantId)
        => Weight.HasValue || (!string.IsNullOrEmpty(variantId) && VariantWeights?.ContainsKey(variantId) == true);

    /// <summary>
    /// Gets the size of one unit of a variant, or of the product when the variant has none of its own, or
    /// <see langword="null"/> when its length, width and height aren't all known.
    /// </summary>
    /// <param name="variantId">The variant's id, or <see langword="null"/> for the product.</param>
    public ShippingDimensions GetDimensions(string variantId)
    {
        if (!string.IsNullOrEmpty(variantId) && VariantDimensions?.TryGetValue(variantId, out var dimensions) == true && dimensions is not null)
        {
            return dimensions;
        }

        return Length is { } length && Width is { } width && Height is { } height
            ? new ShippingDimensions
            {
                Length = length,
                Width = width,
                Height = height,
            }
            : null;
    }

    /// <summary>
    /// Gets the weight of one unit of a variant, or of the product when the variant has none of its own.
    /// </summary>
    public decimal GetWeight(string variantId)
        => !string.IsNullOrEmpty(variantId) && VariantWeights?.TryGetValue(variantId, out var weight) == true
        ? weight
        : Weight ?? 0;
}
