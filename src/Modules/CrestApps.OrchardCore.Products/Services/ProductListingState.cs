using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// What a visitor picked on a listing page, read from and written back to the page's query string.
/// </summary>
/// <remarks>
/// The URL is the whole state, so every filtered view can be bookmarked and shared, the back button works,
/// and the page works without JavaScript. A facet uses its short key (<c>make=toyota&amp;make=honda</c>), and
/// a range facet uses <c>{key}_min</c> and <c>{key}_max</c>. Anything the listing does not define is ignored.
/// </remarks>
public sealed class ProductListingState
{
    /// <summary>
    /// The most values a visitor can pick in one facet. More is never a real visitor, and a cap keeps the SQL bounded.
    /// </summary>
    public const int MaxValuesPerFacet = 50;

    /// <summary>
    /// The highest page number accepted.
    /// </summary>
    public const int MaxPage = 10_000;

    public const string KeywordKey = "q";
    public const string SortKey = "sort";
    public const string ViewKey = "view";
    public const string PageSizeKey = "page-size";
    public const string PageKey = "page";
    public const string MinSuffix = "_min";

    private static readonly HashSet<string> _reservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        KeywordKey,
        SortKey,
        ViewKey,
        PageSizeKey,
        PageKey,
    };

    /// <summary>
    /// Whether a key is one of the listing's own parameters, which a filter cannot use.
    /// </summary>
    public static bool IsReservedKey(string key)
        => !string.IsNullOrEmpty(key) && _reservedKeys.Contains(key);
    public const string MaxSuffix = "_max";

    public Dictionary<string, List<string>> Values { get; private set; } = new(StringComparer.Ordinal);

    public Dictionary<string, (decimal? Min, decimal? Max)> Ranges { get; private set; } = new(StringComparer.Ordinal);

    public string Keyword { get; set; }

    public string Sort { get; set; }

    public ProductListingView? View { get; set; }

    public int? PageSize { get; set; }

    public int Page { get; set; } = 1;

    /// <summary>
    /// Reads the state from a query string, keeping only what the listing defines.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="facets">The listing's facets.</param>
    /// <param name="sortKeys">The listing's sort keys.</param>
    /// <param name="pageSizes">The listing's page sizes.</param>
    public static ProductListingState Parse(IQueryCollection query, IEnumerable<ProductListingFacet> facets, IEnumerable<string> sortKeys, IEnumerable<int> pageSizes)
    {
        var state = new ProductListingState();

        var keyword = query[KeywordKey].ToString().Trim();
        state.Keyword = keyword.Length > 200 ? keyword[..200] : keyword;

        var sort = query[SortKey].ToString();
        if (sortKeys.Contains(sort, StringComparer.Ordinal))
        {
            state.Sort = sort;
        }

        var view = query[ViewKey].ToString();
        if (string.Equals(view, "list", StringComparison.OrdinalIgnoreCase))
        {
            state.View = ProductListingView.List;
        }
        else if (string.Equals(view, "grid", StringComparison.OrdinalIgnoreCase))
        {
            state.View = ProductListingView.Grid;
        }

        if (int.TryParse(query[PageSizeKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && pageSizes.Contains(size))
        {
            state.PageSize = size;
        }

        if (int.TryParse(query[PageKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page))
        {
            state.Page = Math.Clamp(page, 1, MaxPage);
        }

        foreach (var facet in facets)
        {
            if (facet.Kind == ProductFacetKind.Range)
            {
                var min = ParseNumber(query[facet.Key + MinSuffix]);
                var max = ParseNumber(query[facet.Key + MaxSuffix]);

                if (min.HasValue || max.HasValue)
                {
                    if (min > max)
                    {
                        (min, max) = (max, min);
                    }

                    state.Ranges[facet.Key] = (min, max);
                }

                continue;
            }

            var values = query[facet.Key]
                .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => value.Length <= 255)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxValuesPerFacet)
                .ToList();

            if (facet.Kind == ProductFacetKind.Radio && values.Count > 1)
            {
                values = [values[0]];
            }

            if (facet.Kind == ProductFacetKind.Toggle)
            {
                values = values.Contains("true", StringComparer.OrdinalIgnoreCase) ? ["true"] : [];
            }

            if (values.Count > 0)
            {
                state.Values[facet.Key] = values;
            }
        }

        return state;
    }

    public ProductListingState Clone()
        => new()
        {
            Values = Values.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal),
            Ranges = new Dictionary<string, (decimal? Min, decimal? Max)>(Ranges, StringComparer.Ordinal),
            Keyword = Keyword,
            Sort = Sort,
            View = View,
            PageSize = PageSize,
            Page = Page,
        };

    /// <summary>
    /// A copy with one value of a facet added or removed. Any change to the filters returns to the first page.
    /// </summary>
    public ProductListingState Toggle(string facetKey, string value, bool singleValue)
    {
        var state = Clone();
        state.Page = 1;

        if (!state.Values.TryGetValue(facetKey, out var values))
        {
            values = [];
            state.Values[facetKey] = values;
        }

        if (values.Remove(value))
        {
            if (values.Count == 0)
            {
                state.Values.Remove(facetKey);
            }
        }
        else
        {
            if (singleValue)
            {
                values.Clear();
            }

            values.Add(value);
        }

        return state;
    }

    /// <summary>
    /// A copy without any selection in one facet.
    /// </summary>
    public ProductListingState ClearFacet(string facetKey)
    {
        var state = Clone();
        state.Page = 1;
        state.Values.Remove(facetKey);
        state.Ranges.Remove(facetKey);

        return state;
    }

    /// <summary>
    /// A copy without any filter or keyword, keeping the sort, the layout and the page size.
    /// </summary>
    public ProductListingState ClearFilters()
    {
        var state = Clone();
        state.Page = 1;
        state.Values.Clear();
        state.Ranges.Clear();
        state.Keyword = null;

        return state;
    }

    public ProductListingState WithPage(int page)
    {
        var state = Clone();
        state.Page = page;

        return state;
    }

    public ProductListingState WithView(ProductListingView view)
    {
        var state = Clone();
        state.View = view;

        return state;
    }

    /// <summary>
    /// The state as a query string, starting with <c>?</c>, or an empty string when nothing is picked.
    /// </summary>
    public string ToQueryString()
    {
        var builder = new QueryBuilder();

        if (!string.IsNullOrEmpty(Keyword))
        {
            builder.Add(KeywordKey, Keyword);
        }

        foreach (var (key, values) in Values)
        {
            foreach (var value in values)
            {
                builder.Add(key, value);
            }
        }

        foreach (var (key, range) in Ranges)
        {
            if (range.Min.HasValue)
            {
                builder.Add(key + MinSuffix, range.Min.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (range.Max.HasValue)
            {
                builder.Add(key + MaxSuffix, range.Max.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (!string.IsNullOrEmpty(Sort))
        {
            builder.Add(SortKey, Sort);
        }

        if (View.HasValue)
        {
            builder.Add(ViewKey, View.Value == ProductListingView.List ? "list" : "grid");
        }

        if (PageSize.HasValue)
        {
            builder.Add(PageSizeKey, PageSize.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Page > 1)
        {
            builder.Add(PageKey, Page.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToQueryString().ToString();
    }

    private static decimal? ParseNumber(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
}
