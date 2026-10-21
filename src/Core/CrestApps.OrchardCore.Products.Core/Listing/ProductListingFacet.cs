
namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// One filter offered on a listing page.
/// </summary>
public sealed class ProductListingFacet
{
    /// <summary>
    /// Gets or sets the short key the filter uses in the page's URL, such as <c>make</c> or <c>year</c>.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the attribute the filter works on: a field name or a key from <see cref="ProductAttributeKeys"/>.
    /// </summary>
    public string AttributeKey { get; set; }

    /// <summary>
    /// Gets or sets the label shown above the filter. The attribute's label is used when it is empty.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets how the filter is presented and what it matches.
    /// </summary>
    public ProductFacetKind Kind { get; set; }

    /// <summary>
    /// Gets or sets, for a taxonomy attribute, the term depth the filter offers: 1 for top-level terms such as
    /// a make, 2 for their children such as a model. Every term is offered when it is empty.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Gets or sets the key of another filter that must have a selection before this one is shown, such as
    /// the make filter for the model filter.
    /// </summary>
    public string DependsOn { get; set; }

    /// <summary>
    /// Gets or sets whether the filter starts collapsed.
    /// </summary>
    public bool Collapsed { get; set; }

    /// <summary>
    /// Gets or sets how many options are shown before a "See more" link. Every option is shown when it is 0.
    /// </summary>
    public int MaxVisible { get; set; } = 8;

    /// <summary>
    /// Gets or sets the name of the style the filter is drawn in: one of <see cref="ProductFacetStyles"/> or a
    /// style another module registered in <see cref="ProductFacetStyleOptions"/>. A list is drawn when it is empty.
    /// </summary>
    public string Style { get; set; }

    /// <summary>
    /// Gets or sets whether a range filter offers ready-made ranges, such as "Up to $50", above its inputs.
    /// </summary>
    public bool ShowRanges { get; set; }

    /// <summary>
    /// Gets or sets the ready-made ranges a range filter offers. Round ranges are worked out from the products'
    /// values when it is empty.
    /// </summary>
    public List<ProductFacetRange> Ranges { get; set; } = [];
}
