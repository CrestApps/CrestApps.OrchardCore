using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Turns a content item into a product listing page: which products it shows, how visitors can filter and
/// sort them, and how the results are laid out.
/// </summary>
/// <remarks>
/// A site can have as many listing pages as it needs (<em>Cars for sale</em>, <em>Homes for rent</em>), each
/// with its own URL and menu entry, the way Orchard does blogs and lists.
/// </remarks>
public sealed class ProductListingPart : ContentPart
{
    /// <summary>
    /// Gets or sets the product content types the page lists. Every type with a product part is listed when empty.
    /// </summary>
    public string[] ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether visitors can filter the products. When off, the page lists its products without
    /// filters and ignores filters in the address.
    /// </summary>
    public bool ShowFilters { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the page offers a filter for every filterable attribute of its content types, in
    /// field order, instead of the configured <see cref="Facets"/>.
    /// </summary>
    public bool AutomaticFacets { get; set; } = true;

    /// <summary>
    /// Gets or sets the filters offered, in the order they are shown.
    /// </summary>
    public List<ProductListingFacet> Facets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether visitors can change the sort. When off, the products are always in the order of the
    /// <see cref="DefaultSort"/>.
    /// </summary>
    public bool ShowSorting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the page offers the standard sorts plus every sortable attribute, instead of the
    /// configured <see cref="Sorts"/>.
    /// </summary>
    public bool AutomaticSorts { get; set; } = true;

    /// <summary>
    /// Gets or sets the sort options offered, in the order they are listed.
    /// </summary>
    public List<ProductListingSort> Sorts { get; set; } = [];

    /// <summary>
    /// Gets or sets the key of the sort applied when the visitor picks none.
    /// </summary>
    public string DefaultSort { get; set; }

    /// <summary>
    /// Gets or sets the layout the results open in.
    /// </summary>
    public ProductListingView DefaultView { get; set; }

    /// <summary>
    /// Gets or sets whether visitors may switch between the grid and the list layouts.
    /// </summary>
    public bool AllowViewSwitching { get; set; } = true;

    /// <summary>
    /// Gets or sets the shape of product images on the listing's cards.
    /// </summary>
    public ProductImageAspectRatio ImageAspectRatio { get; set; }

    /// <summary>
    /// Gets or sets the page sizes visitors can pick from.
    /// </summary>
    public int[] PageSizes { get; set; } = [12, 24, 48];

    /// <summary>
    /// Gets or sets how visitors move through the pages: numbered pages, a "Load more" button, or infinite scroll.
    /// </summary>
    public ProductListingPaging Paging { get; set; }

    /// <summary>
    /// Gets or sets the page size used when the visitor picks none.
    /// </summary>
    public int DefaultPageSize { get; set; } = 24;

    /// <summary>
    /// Gets or sets whether visitors can search the listing by keyword.
    /// </summary>
    public bool EnableKeywordSearch { get; set; } = true;
}
