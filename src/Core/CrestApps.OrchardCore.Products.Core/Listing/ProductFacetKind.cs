namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// How a filter is presented and what it matches.
/// </summary>
public enum ProductFacetKind
{
    /// <summary>
    /// A list of options with counts. Several can be picked, and a product matches any of them.
    /// </summary>
    Checkbox = 0,

    /// <summary>
    /// A list of options with counts, of which one can be picked.
    /// </summary>
    Radio = 1,

    /// <summary>
    /// A minimum and a maximum, for numbers such as a price or a year.
    /// </summary>
    Range = 2,

    /// <summary>
    /// A single switch that keeps only the products for which a yes/no attribute is true.
    /// </summary>
    Toggle = 3,
}
