using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Products.Elasticsearch.Services;

/// <summary>
/// Searches an Elasticsearch product search index.
/// </summary>
/// <remarks>
/// One request answers the page and every filter: the product types, culture and keyword narrow the query; the
/// selected filters are a post filter, so each facet's aggregation can apply every filter but its own and count
/// what each other option would add.
/// </remarks>
public sealed class ElasticsearchProductSearchService : IProductIndexSearchService
{
    // Enough for every option of a filter; a listing shows a handful and hides the rest behind "Show more".
    private const int MaxFacetValues = 1000;

    private readonly ElasticsearchClient _client;

    public ElasticsearchProductSearchService(ElasticsearchClient client)
    {
        _client = client;
    }

    public async Task<ProductSearchResult> SearchAsync(IndexProfile indexProfile, ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(query);

        var indexQuery = ProductSearchIndexQuery.Create(query);
        var facets = indexQuery.Facets;

        var request = BuildRequest(indexProfile.IndexFullName, indexQuery, facets);
        var response = await _client.SearchAsync<JsonObject>(request, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"The Elasticsearch index '{indexProfile.IndexFullName}' could not be searched: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}");
        }

        var results = new Dictionary<string, ProductFacetResult>(StringComparer.Ordinal);

        for (var i = 0; i < facets.Count; i++)
        {
            var facet = facets[i];
            var aggregate = response.Aggregations?.GetFilter(AggregationName(i));

            if (aggregate is null)
            {
                continue;
            }

            if (facet.IsRange)
            {
                var min = aggregate.Aggregations?.GetMin("min")?.Value;
                var max = aggregate.Aggregations?.GetMax("max")?.Value;

                results[facet.Key] = new ProductFacetResult
                {
                    Key = facet.Key,
                    Min = min is { } low ? (decimal)low : null,
                    Max = max is { } high ? (decimal)high : null,
                };

                continue;
            }

            var counts = aggregate.Aggregations?.GetStringTerms("values")?.Buckets
                .Select(bucket => new KeyValuePair<string, long>(bucket.Key.TryGetString(out var token) ? token : null, bucket.DocCount))
                .Where(pair => pair.Key is not null) ?? [];

            results[facet.Key] = ProductSearchIndexFields.ToFacetResult(facet, counts);
        }

        return new ProductSearchResult
        {
            ContentItemIds = response.Hits.Select(hit => hit.Id).Where(id => !string.IsNullOrEmpty(id)).ToArray(),
            Total = (int)Math.Min(response.Total, int.MaxValue),
            Facets = results,
        };
    }

    internal static SearchRequest BuildRequest(string indexFullName, ProductSearchIndexQuery indexQuery, IReadOnlyList<ProductFacetRequest> facets)
    {
        var aggregations = new Dictionary<string, Aggregation>(StringComparer.Ordinal);

        for (var i = 0; i < facets.Count; i++)
        {
            var facet = facets[i];

            // Every selected filter but the facet's own.
            Aggregation aggregation = AllOf(indexQuery.FiltersExcept(facet.Key).Select(ToQuery));

            aggregation.Aggregations = facet.IsRange
                ? new Dictionary<string, Aggregation>
                {
                    ["min"] = new MinAggregation { Field = ProductSearchIndexFields.Numbers(facet.AttributeKey) },
                    ["max"] = new MaxAggregation { Field = ProductSearchIndexFields.Numbers(facet.AttributeKey) },
                }
                : new Dictionary<string, Aggregation>
                {
                    ["values"] = new TermsAggregation { Field = ProductSearchIndexFields.Facets(facet.AttributeKey), Size = MaxFacetValues },
                };

            aggregations[AggregationName(i)] = aggregation;
        }

        return new SearchRequest(indexFullName)
        {
            Query = AllOf(BaseQueries(indexQuery)),
            PostFilter = indexQuery.Filters.Count == 0 ? null : AllOf(indexQuery.Filters.Select(ToQuery)),
            Aggregations = aggregations,
            Sort = indexQuery.Sort.Select(ToSort).ToList(),
            From = indexQuery.Skip,
            Size = indexQuery.Take,
            Source = new SourceConfig(false),
            TrackTotalHits = new TrackHits(true),
        };
    }

    // The product types, the culture and the keyword: what every facet counts within.
    private static IEnumerable<Query> BaseQueries(ProductSearchIndexQuery indexQuery)
    {
        if (indexQuery.ContentTypes.Count > 0)
        {
            yield return AnyOf(ProductSearchIndexFields.ContentType, indexQuery.ContentTypes);
        }

        if (indexQuery.Cultures.Count > 0)
        {
            yield return AnyOf(ProductSearchIndexFields.Culture, indexQuery.Cultures);
        }

        if (indexQuery.Keyword is not null)
        {
            // Every word must start a word of the title, SKU or a searchable value; or the keyword is the SKU.
            yield return new BoolQuery
            {
                Should =
                [
                    new MatchBoolPrefixQuery { Field = ProductSearchIndexFields.Keywords, Query = indexQuery.Keyword, Operator = Operator.And },
                    new TermQuery { Field = ProductSearchIndexFields.Sku, Value = indexQuery.Keyword },
                ],
                MinimumShouldMatch = 1,
            };
        }
    }

    private static Query ToQuery(ProductIndexFilter filter)
        => filter.IsRange
            ? new NumberRangeQuery(filter.Field)
            {
                Gte = filter.Min is { } min ? (double)min : null,
                Lte = filter.Max is { } max ? (double)max : null,
            }
            : AnyOf(filter.Field, filter.Values);

    private static TermsQuery AnyOf(string field, IReadOnlyList<string> values)
        => new()
        {
            Field = field,
            Terms = new TermsQueryField(values.Select(FieldValue.String).ToArray()),
        };

    private static Query AllOf(IEnumerable<Query> queries)
    {
        var filters = queries.ToList();

        return filters.Count == 0 ? new MatchAllQuery() : new BoolQuery { Filter = filters };
    }

    // Products without a value sort last in either direction.
    private static SortOptions ToSort(ProductIndexSortField sort)
        => new FieldSort(sort.Field)
        {
            Order = sort.Descending ? SortOrder.Desc : SortOrder.Asc,
            Missing = "_last",
        };

    private static string AggregationName(int index)
        => $"facet{index}";
}
