namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The layouts a listing page can show its results in.
/// </summary>
public enum ProductListingView
{
    /// <summary>
    /// Cards in a responsive grid.
    /// </summary>
    Grid = 0,

    /// <summary>
    /// One wide row per product, with its specifications.
    /// </summary>
    List = 1,
}
