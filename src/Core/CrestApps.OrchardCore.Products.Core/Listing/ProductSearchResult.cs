namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The products that matched a <see cref="ProductSearchQuery"/>, in order, with the facet counts it asked for.
/// </summary>
public sealed class ProductSearchResult
{
    /// <summary>
    /// Gets or sets the content item identifiers of the requested page, in order.
    /// </summary>
    public IReadOnlyList<string> ContentItemIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many products matched in total.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the facet results, keyed by facet key.
    /// </summary>
    public IReadOnlyDictionary<string, ProductFacetResult> Facets { get; set; } = new Dictionary<string, ProductFacetResult>();
}

/// <summary>
/// The options of one facet and how many products each would match.
/// </summary>
public sealed class ProductFacetResult
{
    /// <summary>
    /// Gets or sets the facet key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the options with their counts.
    /// </summary>
    public IReadOnlyList<ProductFacetValue> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets, for a range facet, the smallest value among the matching products.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets, for a range facet, the largest value among the matching products.
    /// </summary>
    public decimal? Max { get; set; }
}

/// <summary>
/// One option of a facet.
/// </summary>
public sealed class ProductFacetValue
{
    /// <summary>
    /// Gets or sets the value the option filters on.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the text shown for the option.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets how many products the option would match.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the option's defined position, such as a term's place in its taxonomy.
    /// </summary>
    public int Order { get; set; }
}
