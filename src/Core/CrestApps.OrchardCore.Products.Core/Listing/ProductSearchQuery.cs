namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// What a listing asks the search service for: which products, filtered and sorted how, which page, and
/// which facets to count.
/// </summary>
public sealed class ProductSearchQuery
{
    /// <summary>
    /// Gets or sets the content types to search.
    /// </summary>
    public IList<string> ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the filters. Filters on different facets must all match; the values of one filter match
    /// when any of them does.
    /// </summary>
    public IList<ProductSearchFilter> Filters { get; set; } = [];

    /// <summary>
    /// Gets or sets the facets whose option counts are wanted.
    /// </summary>
    public IList<ProductFacetRequest> Facets { get; set; } = [];

    /// <summary>
    /// Gets or sets the keyword to match against titles, SKUs and searchable attributes.
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Gets or sets the culture to list: products localized in it, and products that are not localized. Every product
    /// is listed when it is empty.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the sort, or <see langword="null"/> for newest first.
    /// </summary>
    public ProductListingSort Sort { get; set; }

    /// <summary>
    /// Gets or sets how many products to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many products to return.
    /// </summary>
    public int Take { get; set; } = 24;
}

/// <summary>
/// One filter of a <see cref="ProductSearchQuery"/>.
/// </summary>
public sealed class ProductSearchFilter
{
    /// <summary>
    /// Gets or sets the key of the facet the filter came from. A facet's own filter is left out when its
    /// options are counted, so a visitor who picked one make still sees how many of the others there are.
    /// </summary>
    public string FacetKey { get; set; }

    /// <summary>
    /// Gets or sets the attribute the filter works on.
    /// </summary>
    public string AttributeKey { get; set; }

    /// <summary>
    /// Gets or sets the values, any of which matches.
    /// </summary>
    public IList<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the smallest numeric value that matches.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the largest numeric value that matches.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets a value indicating whether the filter is a numeric range.
    /// </summary>
    public bool IsRange => Min.HasValue || Max.HasValue;
}

/// <summary>
/// A facet whose option counts a <see cref="ProductSearchQuery"/> asks for.
/// </summary>
public sealed class ProductFacetRequest
{
    /// <summary>
    /// Gets or sets the facet key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the attribute counted.
    /// </summary>
    public string AttributeKey { get; set; }

    /// <summary>
    /// Gets or sets, for a taxonomy attribute, the term depth counted.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Gets or sets whether the facet is a numeric range, for which the smallest and largest values are
    /// returned instead of option counts.
    /// </summary>
    public bool IsRange { get; set; }
}
