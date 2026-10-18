using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Applies a product's volume and role discounts to a unit price. The best single discount the buyer qualifies for
/// applies, to the price the product sells at then (its sale price during a sale); discounts don't add up.
/// </summary>
public static class ProductVolumePricing
{
    /// <summary>
    /// Returns the best percentage off a buyer in the roles gets for the quantity, or zero.
    /// </summary>
    public static decimal GetPercentOff(ProductPart product, int quantity, IEnumerable<string> roles)
    {
        if (product is null)
        {
            return 0m;
        }

        var tier = (product.PriceTiers ?? [])
            .Where(entry => entry.MinQuantity > 0 && quantity >= entry.MinQuantity)
            .Select(entry => entry.PercentOff)
            .DefaultIfEmpty(0m)
            .Max();

        var held = roles?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var role = (product.RoleDiscounts ?? [])
            .Where(entry => !string.IsNullOrEmpty(entry.Role) && held.Contains(entry.Role))
            .Select(entry => entry.PercentOff)
            .DefaultIfEmpty(0m)
            .Max();

        return Math.Clamp(Math.Max(tier, role), 0m, 100m);
    }

    /// <summary>
    /// Returns the unit price after the best discount, rounded to cents.
    /// </summary>
    public static decimal Apply(decimal unitPrice, decimal percentOff)
        => percentOff <= 0m ? unitPrice : Math.Round(unitPrice * (100m - percentOff) / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Returns whether the quantity follows the product's rules, and the rule it breaks when it doesn't.
    /// </summary>
    public static ProductQuantityRule Check(ProductPart product, int quantity)
    {
        if (product is null)
        {
            return ProductQuantityRule.None;
        }

        if (product.MinQuantity is > 1 and var min && quantity < min)
        {
            return ProductQuantityRule.BelowMinimum;
        }

        if (product.QuantityStep is > 1 and var step && quantity % step != 0)
        {
            return ProductQuantityRule.NotAMultiple;
        }

        return ProductQuantityRule.None;
    }

    /// <summary>
    /// Returns the quantity to start the quantity box at: the minimum, rounded up to a pack.
    /// </summary>
    public static int GetStartingQuantity(ProductPart product)
    {
        var quantity = Math.Max(1, product?.MinQuantity ?? 1);
        var step = Math.Max(1, product?.QuantityStep ?? 1);

        return quantity % step == 0 ? quantity : ((quantity / step) + 1) * step;
    }
}

/// <summary>
/// The quantity rule a cart line breaks.
/// </summary>
public enum ProductQuantityRule
{
    None,
    BelowMinimum,
    NotAMultiple,
}
