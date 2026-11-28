using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Core.Models;
using OrchardCore.Indexing.Models;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Finds index profiles for the index tools and describes them with an explicit list of fields.
/// </summary>
internal static class IndexProfileContent
{
    /// <summary>
    /// The most index profiles one call of a tool synchronizes, rebuilds or deletes.
    /// </summary>
    public const int MaxProfiles = 20;

    /// <summary>
    /// The most indexed field names a tool returns for one index profile.
    /// </summary>
    public const int MaxFields = 300;

    /// <summary>
    /// Finds an index profile by its ID, then by its name. Both are unique on a site.
    /// </summary>
    /// <param name="indexProfileManager">The index profile manager.</param>
    /// <param name="idOrName">The ID or the name of the index profile.</param>
    /// <returns>The index profile, or <see langword="null"/> when there is none.</returns>
    public static async Task<IndexProfile> FindAsync(IIndexProfileManager indexProfileManager, string idOrName)
    {
        ArgumentNullException.ThrowIfNull(indexProfileManager);

        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        var key = idOrName.Trim();

        return await indexProfileManager.FindByIdAsync(key)
            ?? await indexProfileManager.FindByNameAsync(key);
    }

    /// <summary>
    /// Reads the <c>indexes</c> argument of a tool that acts on several index profiles and finds each one.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="indexProfileManager">The index profile manager.</param>
    /// <returns>
    /// The index profiles that were found, the given IDs or names that match none, and a sentence that says what is
    /// wrong with the argument, or <see langword="null"/> when it can be used.
    /// </returns>
    public static async Task<(List<IndexProfile> Found, List<string> NotFound, string Error)> ResolveAsync(AIFunctionArguments arguments, IIndexProfileManager indexProfileManager)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(indexProfileManager);

        var found = new List<IndexProfile>();
        var notFound = new List<string>();

        if (!arguments.TryGetFirst<string[]>("indexes", out var indexes) || indexes is not { Length: > 0 })
        {
            return (found, notFound, "indexes must be a list of IDs or names of index profiles. Use listIndexProfiles to get them.");
        }

        if (indexes.Length > MaxProfiles)
        {
            return (found, notFound, $"one call takes at most {MaxProfiles} index profiles. Use several calls.");
        }

        foreach (var index in indexes)
        {
            var indexProfile = await FindAsync(indexProfileManager, index);

            if (indexProfile is null)
            {
                notFound.Add(index);
            }
            else if (!found.Exists(known => known.Id == indexProfile.Id))
            {
                found.Add(indexProfile);
            }
        }

        return (found, notFound, null);
    }

    /// <summary>
    /// Whether an index profile indexes content items.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <returns><see langword="true"/> when the type of the index profile is the content type.</returns>
    public static bool IndexesContent(IndexProfile indexProfile)
        => string.Equals(IndexingConstants.ContentsIndexSource, indexProfile.Type, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Describes an index profile for a list.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The fields of the index profile a list shows.</returns>
    public static async Task<object> DescribeSummaryAsync(IndexProfile indexProfile, ILocalClock localClock)
    {
        return new
        {
            id = indexProfile.Id,
            name = indexProfile.Name,
            provider = indexProfile.ProviderName,
            type = indexProfile.Type,
            indexName = indexProfile.IndexName,
            indexedContentTypes = GetContentSettings(indexProfile)?.IndexedContentTypes,
            created = await GetCreatedAsync(indexProfile, localClock),
        };
    }

    /// <summary>
    /// Describes an index profile with its settings and the names of its indexed fields.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The fields of the index profile.</returns>
    public static async Task<object> DescribeAsync(IndexProfile indexProfile, ILocalClock localClock)
    {
        var contentSettings = GetContentSettings(indexProfile);
        var fields = GetFieldNames(indexProfile);

        return new
        {
            id = indexProfile.Id,
            name = indexProfile.Name,
            provider = indexProfile.ProviderName,
            type = indexProfile.Type,
            indexName = indexProfile.IndexName,
            indexFullName = indexProfile.IndexFullName,
            created = await GetCreatedAsync(indexProfile, localClock),
            author = indexProfile.Author,
            content = contentSettings is null
                ? null
                : new
                {
                    indexedContentTypes = contentSettings.IndexedContentTypes ?? [],
                    culture = contentSettings.Culture,
                    indexLatest = contentSettings.IndexLatest,
                },
            providerSettings = DescribeProviderSettings(indexProfile),
            fields = fields?.Take(MaxFields),
            fieldsTotal = fields?.Count,
            fieldsTruncated = fields?.Count > MaxFields
                ? true
                : (bool?)null,
        };
    }

    /// <summary>
    /// Gets the content settings of an index profile that indexes content items.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <returns>The content settings, or <see langword="null"/> when the index profile does not index content items.</returns>
    public static ContentIndexMetadata GetContentSettings(IndexProfile indexProfile)
    {
        if (!IndexesContent(indexProfile))
        {
            return null;
        }

        return indexProfile.TryGet<ContentIndexMetadata>(out var metadata)
            ? metadata
            : new ContentIndexMetadata();
    }

    private static async Task<string> GetCreatedAsync(IndexProfile indexProfile, ILocalClock localClock)
    {
        return indexProfile.CreatedUtc == default
            ? null
            : await AgentToolResults.ToLocalAsync(localClock, indexProfile.CreatedUtc);
    }

    // Each search provider keeps its settings in its own metadata. The settings of the Lucene and Elasticsearch
    // providers are described; another provider's settings are shown on the admin page of the index.
    private static object DescribeProviderSettings(IndexProfile indexProfile)
    {
        if (IsProvider(indexProfile, LuceneConstants.ProviderName))
        {
            indexProfile.TryGet<LuceneIndexMetadata>(out var metadata);
            indexProfile.TryGet<LuceneIndexDefaultQueryMetadata>(out var queryMetadata);

            return new
            {
                analyzer = metadata?.AnalyzerName,
                storeSourceData = metadata?.StoreSourceData,
                queryAnalyzer = queryMetadata?.QueryAnalyzerName,
                allowLuceneQueries = queryMetadata?.AllowLuceneQueries,
                defaultSearchFields = queryMetadata?.DefaultSearchFields,
            };
        }

        if (IsProvider(indexProfile, ElasticsearchConstants.ProviderName))
        {
            indexProfile.TryGet<ElasticsearchIndexMetadata>(out var metadata);
            indexProfile.TryGet<ElasticsearchDefaultQueryMetadata>(out var queryMetadata);

            return new
            {
                analyzer = metadata?.AnalyzerName,
                storeSourceData = metadata?.StoreSourceData,
                queryAnalyzer = queryMetadata?.QueryAnalyzerName,
                searchType = queryMetadata?.SearchType,
                defaultQuery = queryMetadata?.DefaultQuery,
                defaultSearchFields = queryMetadata?.DefaultSearchFields,
            };
        }

        return null;
    }

    private static List<string> GetFieldNames(IndexProfile indexProfile)
    {
        if (IsProvider(indexProfile, LuceneConstants.ProviderName))
        {
            return indexProfile.TryGet<LuceneIndexMetadata>(out var metadata) && metadata.IndexMappings?.Fields is not null
                ? [.. metadata.IndexMappings.Fields.Order(StringComparer.Ordinal)]
                : null;
        }

        if (IsProvider(indexProfile, ElasticsearchConstants.ProviderName))
        {
            return indexProfile.TryGet<ElasticsearchIndexMetadata>(out var metadata) && metadata.IndexMappings?.Mapping?.Properties is not null
                ? [.. metadata.IndexMappings.GetFieldPaths().Order(StringComparer.Ordinal)]
                : null;
        }

        return null;
    }

    private static bool IsProvider(IndexProfile indexProfile, string providerName)
        => string.Equals(indexProfile.ProviderName, providerName, StringComparison.OrdinalIgnoreCase);
}
