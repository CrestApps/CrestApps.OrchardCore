using CrestApps.OrchardCore.Products.Core.Listing;

namespace CrestApps.OrchardCore.Products.Core.Search;

/// <summary>
/// A product search query in terms of index fields, so every provider filters and sorts the way the database
/// search does and only has to write it in its own query language.
/// </summary>
public sealed class ProductSearchIndexQuery
{
    private ProductSearchIndexQuery()
    {
    }

    /// <summary>
    /// The product types to list; empty for every type.
    /// </summary>
    public IReadOnlyList<string> ContentTypes { get; private init; } = [];

    /// <summary>
    /// The cultures a product may have to be listed: the visitor's and <see cref="ProductSearchIndexFields.NeutralCulture"/>.
    /// Empty to list every product.
    /// </summary>
    public IReadOnlyList<string> Cultures { get; private init; } = [];

    /// <summary>
    /// The trimmed keyword, or <see langword="null"/>.
    /// </summary>
    public string Keyword { get; private init; }

    /// <summary>
    /// The filters, each with the key of the facet it came from.
    /// </summary>
    public IReadOnlyList<ProductIndexFilter> Filters { get; private init; } = [];

    /// <summary>
    /// The sort, ties broken the way the database search breaks them. Products without a value sort last.
    /// </summary>
    public IReadOnlyList<ProductIndexSortField> Sort { get; private init; } = [];

    /// <summary>
    /// The facets to count: those of the query that name both a key and an attribute.
    /// </summary>
    public IReadOnlyList<ProductFacetRequest> Facets { get; private init; } = [];

    public int Skip { get; private init; }

    public int Take { get; private init; }

    /// <summary>
    /// The filters that narrow a facet's own counts: every filter but the facet's, so a visitor sees how many
    /// products each other option would add.
    /// </summary>
    public IEnumerable<ProductIndexFilter> FiltersExcept(string facetKey)
        => Filters.Where(filter => !string.Equals(filter.FacetKey, facetKey, StringComparison.Ordinal));

    /// <summary>
    /// Whether one of the filters came from the given facet, so the facet's counts need their own query.
    /// </summary>
    public bool HasFilterFor(string facetKey)
        => Filters.Any(filter => string.Equals(filter.FacetKey, facetKey, StringComparison.Ordinal));

    public static ProductSearchIndexQuery Create(ProductSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = new List<ProductIndexFilter>();

        foreach (var filter in query.Filters ?? [])
        {
            if (filter is null || string.IsNullOrEmpty(filter.AttributeKey))
            {
                continue;
            }

            if (filter.IsRange)
            {
                filters.Add(new ProductIndexFilter
                {
                    FacetKey = filter.FacetKey,
                    AttributeKey = filter.AttributeKey,
                    Field = ProductSearchIndexFields.Numbers(filter.AttributeKey),
                    Min = filter.Min,
                    Max = filter.Max,
                });

                continue;
            }

            var values = filter.Values?.Where(value => !string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

            if (values.Length > 0)
            {
                filters.Add(new ProductIndexFilter
                {
                    FacetKey = filter.FacetKey,
                    AttributeKey = filter.AttributeKey,
                    Field = ProductSearchIndexFields.Values(filter.AttributeKey),
                    Values = values,
                });
            }
        }

        return new ProductSearchIndexQuery
        {
            ContentTypes = query.ContentTypes?.Where(type => !string.IsNullOrEmpty(type)).Distinct(StringComparer.Ordinal).ToArray() ?? [],
            Cultures = string.IsNullOrEmpty(query.Culture) ? [] : [query.Culture, ProductSearchIndexFields.NeutralCulture],
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            Filters = filters,
            Facets = (query.Facets ?? [])
                .Where(facet => !string.IsNullOrEmpty(facet?.Key) && !string.IsNullOrEmpty(facet.AttributeKey))
                .ToArray(),
            Sort = GetSort(query.Sort),
            Skip = Math.Max(0, query.Skip),
            Take = Math.Clamp(query.Take, 1, 200),
        };
    }

    /// <summary>
    /// The sort fields of a listing sort, the way the database search orders products.
    /// </summary>
    public static IReadOnlyList<ProductIndexSortField> GetSort(ProductListingSort sort)
    {
        // Ties, and a listing with no sort, go to featured products first, then the newest.
        ProductIndexSortField[] tieBreak =
        [
            new(ProductSearchIndexFields.Featured, Descending: true),
            new(ProductSearchIndexFields.PublishedUtc, Descending: true),
            new(ProductSearchIndexFields.Sequence, Descending: true),
        ];

        if (sort is null || string.IsNullOrEmpty(sort.AttributeKey))
        {
            return tieBreak;
        }

        if (sort.AttributeKey == ProductAttributeKeys.Featured)
        {
            return sort.Descending ? tieBreak : [new(ProductSearchIndexFields.Featured, Descending: false), .. tieBreak[1..]];
        }

        var column = sort.AttributeKey switch
        {
            ProductAttributeKeys.Price => ProductSearchIndexFields.Price,
            ProductAttributeKeys.Title => ProductSearchIndexFields.TitleSort,
            ProductAttributeKeys.Published => ProductSearchIndexFields.PublishedUtc,
            ProductAttributeKeys.Position => ProductSearchIndexFields.Position,
            _ => null,
        };

        if (column is not null)
        {
            return [new(column, sort.Descending), .. tieBreak];
        }

        // Numbers (and dates and times, which are numbers too) first, then text and terms by the name visitors see.
        return
        [
            new(ProductSearchIndexFields.SortNumber(sort.AttributeKey), sort.Descending),
            new(ProductSearchIndexFields.SortText(sort.AttributeKey), sort.Descending),
            .. tieBreak,
        ];
    }
}

/// <summary>
/// A filter on one index field: any of its values, or a range of numbers.
/// </summary>
public sealed class ProductIndexFilter
{
    public string FacetKey { get; init; }

    public string AttributeKey { get; init; }

    public string Field { get; init; }

    public IReadOnlyList<string> Values { get; init; } = [];

    public decimal? Min { get; init; }

    public decimal? Max { get; init; }

    public bool IsRange => Min.HasValue || Max.HasValue;
}

/// <summary>
/// One field of a sort. Products without a value sort last in either direction.
/// </summary>
public sealed record ProductIndexSortField(string Field, bool Descending);
