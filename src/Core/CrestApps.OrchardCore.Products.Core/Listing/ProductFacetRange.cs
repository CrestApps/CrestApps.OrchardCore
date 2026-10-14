namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// A ready-made range offered above a range filter, such as <em>$25 to $75</em>.
/// </summary>
public sealed class ProductFacetRange
{
    /// <summary>
    /// Gets or sets the lowest value, or <see langword="null"/> for a range with no lower end ("Up to $25").
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the highest value, or <see langword="null"/> for a range with no upper end ("$75 and up").
    /// </summary>
    public decimal? Max { get; set; }
}
