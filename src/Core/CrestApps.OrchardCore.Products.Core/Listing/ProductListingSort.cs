namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// One sort option offered on a listing page.
/// </summary>
public sealed class ProductListingSort
{
    /// <summary>
    /// Gets or sets the short key the sort uses in the page's URL, such as <c>price-asc</c>.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the sort list.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the attribute sorted on: a sortable field name or a key from <see cref="ProductAttributeKeys"/>.
    /// </summary>
    public string AttributeKey { get; set; }

    /// <summary>
    /// Gets or sets whether the highest values come first.
    /// </summary>
    public bool Descending { get; set; }
}
