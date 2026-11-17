using OrchardCore.Elasticsearch;
using OrchardCore.Lucene;
using OrchardCore.Queries;
using OrchardCore.Search.Lucene;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.Search;

/// <summary>
/// The search providers whose indexes take a JSON query: Lucene and Elasticsearch. For both, the query source has the
/// name of the provider.
/// </summary>
internal static class SearchQueryProviders
{
    /// <summary>
    /// Gets the names of the providers, which are also the names of their query sources.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        LuceneConstants.ProviderName,
        ElasticsearchConstants.ProviderName,
    ];

    /// <summary>
    /// Whether a provider or query source is Lucene.
    /// </summary>
    /// <param name="name">The name of the provider or of the query source.</param>
    /// <returns><see langword="true"/> for Lucene.</returns>
    public static bool IsLucene(string name)
        => string.Equals(name, LuceneConstants.ProviderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a provider or query source is Elasticsearch.
    /// </summary>
    /// <param name="name">The name of the provider or of the query source.</param>
    /// <returns><see langword="true"/> for Elasticsearch.</returns>
    public static bool IsElasticsearch(string name)
        => string.Equals(name, ElasticsearchConstants.ProviderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a provider or query source takes a JSON query.
    /// </summary>
    /// <param name="name">The name of the provider or of the query source.</param>
    /// <returns><see langword="true"/> for Lucene and Elasticsearch.</returns>
    public static bool IsSupported(string name)
        => IsLucene(name) || IsElasticsearch(name);

    /// <summary>
    /// Gets the permission the "Run query" admin page of a provider checks.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <returns>The permission, or <see langword="null"/> when the provider has no such page.</returns>
    public static Permission GetRunQueryPermission(string providerName)
    {
        if (IsLucene(providerName))
        {
            return LuceneSearchPermissions.ManageLuceneIndexes;
        }

        return IsElasticsearch(providerName)
            ? ElasticsearchPermissions.ManageElasticIndexes
            : null;
    }

    /// <summary>
    /// Gets the number of documents a search query matched, which can be more than the documents it returned.
    /// </summary>
    /// <param name="results">The results of the query.</param>
    /// <returns>The number of matching documents, or <see langword="null"/> when the results do not say.</returns>
    public static long? GetMatchCount(IQueryResults results)
    {
        return results switch
        {
            LuceneQueryResults luceneResults => luceneResults.Count,
            ElasticsearchQueryResults elasticsearchResults => elasticsearchResults.Count,
            _ => null,
        };
    }
}
