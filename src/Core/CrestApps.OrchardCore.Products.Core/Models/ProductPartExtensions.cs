namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The rules that combine a product's own values with its content type's settings.
/// </summary>
/// <remarks>
/// They are the single place that decides whether a product can be bought and how its price is shown, so the
/// editor, the listing and the checkout cannot disagree.
/// </remarks>
public static class ProductPartExtensions
{
    /// <summary>
    /// Whether the product can be bought online.
    /// </summary>
    /// <param name="part">The product part.</param>
    /// <param name="settings">The content type's product settings, or <see langword="null"/> when there are none.</param>
    public static bool CanBePurchased(this ProductPart part, ProductPartSettings settings)
    {
        ArgumentNullException.ThrowIfNull(part);

        // An external product is bought where it links to, not here.
        if (!string.IsNullOrWhiteSpace(part.ExternalUrl))
        {
            return false;
        }

        return (settings?.SalesMode ?? ProductSalesMode.Purchasable) switch
        {
            ProductSalesMode.ListingOnly => false,
            ProductSalesMode.PerItem => part.IsPurchasable,
            _ => true,
        };
    }

    /// <summary>
    /// How the product's price is shown.
    /// </summary>
    /// <param name="part">The product part.</param>
    /// <param name="settings">The content type's product settings, or <see langword="null"/> when there are none.</param>
    public static ProductPriceDisplay GetPriceDisplay(this ProductPart part, ProductPartSettings settings)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (settings is not null && settings.AllowPriceDisplayOverride && part.PriceDisplay.HasValue)
        {
            return part.PriceDisplay.Value;
        }

        return settings?.PriceDisplay ?? ProductPriceDisplay.Show;
    }

    /// <summary>
    /// Whether the product must carry a price: it can be bought, or its price is shown.
    /// </summary>
    /// <param name="part">The product part.</param>
    /// <param name="settings">The content type's product settings, or <see langword="null"/> when there are none.</param>
    public static bool RequiresPrice(this ProductPart part, ProductPartSettings settings)
        => part.CanBePurchased(settings) || part.GetPriceDisplay(settings) == ProductPriceDisplay.Show;
}
