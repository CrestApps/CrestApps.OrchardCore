using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.AzureAI.Services;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Products.AzureAI.Services;

/// <summary>
/// Searches an Azure AI Search product search index.
/// </summary>
/// <remarks>
/// The page comes with the counts of every filter nobody has picked from. A filter with a selection is counted by
/// its own request, without that selection, so the other options say how many products they add; a range's bounds
/// come from the first product each way.
/// </remarks>
public sealed class AzureAIProductSearchService : IProductIndexSearchService
{
    private const int MaxFacetValues = 1000;

    private readonly AzureAIClientFactory _clientFactory;

    public AzureAIProductSearchService(AzureAIClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<ProductSearchResult> SearchAsync(IndexProfile indexProfile, ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(query);

        var client = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);
        var indexQuery = ProductSearchIndexQuery.Create(query);
        var searchText = AzureAIProductSearchFields.GetSearchText(indexQuery.Keyword);
        var facets = indexQuery.Facets;

        var options = CreateOptions(indexQuery, excludedFacetKey: null);
        options.Skip = indexQuery.Skip;
        options.Size = indexQuery.Take;
        options.IncludeTotalCount = true;
        options.Select.Add(ProductSearchIndexFields.ContentItemId);

        foreach (var sort in AzureAIProductSearchFields.GetOrderBy(indexQuery.Sort))
        {
            options.OrderBy.Add(sort);
        }

        foreach (var facet in facets.Where(facet => !facet.IsRange && !indexQuery.HasFilterFor(facet.Key)))
        {
            options.Facets.Add($"{ProductSearchIndexFields.Facets(facet.AttributeKey)},count:{MaxFacetValues}");
        }

        var response = (await client.SearchAsync<SearchDocument>(searchText, options, cancellationToken)).Value;

        var ids = new List<string>();

        await foreach (var result in response.GetResultsAsync())
        {
            if (result.Document.TryGetValue(ProductSearchIndexFields.ContentItemId, out var id) && id is string contentItemId)
            {
                ids.Add(contentItemId);
            }
        }

        var results = new Dictionary<string, ProductFacetResult>(StringComparer.Ordinal);

        foreach (var facet in facets)
        {
            if (facet.IsRange)
            {
                results[facet.Key] = new ProductFacetResult
                {
                    Key = facet.Key,
                    Min = await GetBoundAsync(client, searchText, indexQuery, facet, ProductSearchIndexFields.SortNumber(facet.AttributeKey), descending: false, cancellationToken),
                    Max = await GetBoundAsync(client, searchText, indexQuery, facet, ProductSearchIndexFields.MaxNumber(facet.AttributeKey), descending: true, cancellationToken),
                };

                continue;
            }

            var field = ProductSearchIndexFields.Facets(facet.AttributeKey);
            var counts = response.Facets;

            if (indexQuery.HasFilterFor(facet.Key))
            {
                var facetOptions = CreateOptions(indexQuery, facet.Key);
                facetOptions.Size = 0;
                facetOptions.Facets.Add($"{field},count:{MaxFacetValues}");

                counts = (await client.SearchAsync<SearchDocument>(searchText, facetOptions, cancellationToken)).Value.Facets;
            }

            var tokens = counts is not null && counts.TryGetValue(field, out var values)
                ? values.Select(value => new KeyValuePair<string, long>(value.Value as string, value.Count ?? 0)).Where(pair => pair.Key is not null)
                : [];

            results[facet.Key] = ProductSearchIndexFields.ToFacetResult(facet, tokens);
        }

        return new ProductSearchResult
        {
            ContentItemIds = ids,
            Total = (int)Math.Min(response.TotalCount ?? ids.Count, int.MaxValue),
            Facets = results,
        };
    }

    // The smallest (or largest) number of an attribute among the products the facet counts within.
    private static async Task<decimal?> GetBoundAsync(
        SearchClient client,
        string searchText,
        ProductSearchIndexQuery indexQuery,
        ProductFacetRequest facet,
        string field,
        bool descending,
        CancellationToken cancellationToken)
    {
        var options = new SearchOptions
        {
            Filter = AzureAIProductSearchFields.GetFilter(indexQuery, facet.Key, requiredField: field),
            Size = 1,
            SearchMode = SearchMode.All,
            QueryType = SearchQueryType.Simple,
        };

        options.SearchFields.Add(ProductSearchIndexFields.Title);
        options.SearchFields.Add(ProductSearchIndexFields.Keywords);
        options.Select.Add(field);
        options.OrderBy.Add($"{field} {(descending ? "desc" : "asc")}");

        var response = (await client.SearchAsync<SearchDocument>(searchText, options, cancellationToken)).Value;

        await foreach (var result in response.GetResultsAsync())
        {
            if (result.Document.TryGetValue(field, out var value) && value is not null)
            {
                return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static SearchOptions CreateOptions(ProductSearchIndexQuery indexQuery, string excludedFacetKey)
    {
        var options = new SearchOptions
        {
            Filter = AzureAIProductSearchFields.GetFilter(indexQuery, excludedFacetKey),
            SearchMode = SearchMode.All,
            QueryType = SearchQueryType.Simple,
        };

        options.SearchFields.Add(ProductSearchIndexFields.Title);
        options.SearchFields.Add(ProductSearchIndexFields.Keywords);

        return options;
    }
}
