namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// How the options of a checkbox or radio filter are drawn.
/// </summary>
public enum ProductFacetDisplay
{
    /// <summary>
    /// A list of options with their counts.
    /// </summary>
    List = 0,

    /// <summary>
    /// A row of buttons, which suits short values such as sizes.
    /// </summary>
    Buttons = 1,

    /// <summary>
    /// A row of color swatches, drawn from option names that are CSS colors (black, navy, #1e90ff).
    /// </summary>
    Swatches = 2,
}
