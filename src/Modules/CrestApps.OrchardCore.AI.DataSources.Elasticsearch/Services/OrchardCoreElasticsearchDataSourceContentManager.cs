using System.Text.Json.Nodes;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

internal sealed class OrchardCoreElasticsearchDataSourceContentManager : IDataSourceContentManager
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger _logger;

    internal static List<(string Kind, string Value)> BuildMustQueryDebug(string dataSourceId, string filter)
    {
        var queries = new List<(string Kind, string Value)>
        {
            ("term", dataSourceId),
        };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queries.Add(("wrapper", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(filter))));
        }

        return queries;
    }

    public OrchardCoreElasticsearchDataSourceContentManager(
        ElasticsearchClient elasticClient,
        ILogger<OrchardCoreElasticsearchDataSourceContentManager> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);

        if (embedding.Length == 0)
        {
            return [];
        }

        try
        {
            var mustQueries = new List<Action<QueryDescriptor<JsonObject>>>();

            foreach (var query in BuildMustQueryDebug(dataSourceId, filter))
            {
                if (query.Kind == "term")
                {
                    mustQueries.Add(m => m.Term(t => t.Field(DataSourceConstants.ColumnNames.DataSourceId).Value(query.Value)));
                }
                else if (query.Kind == "wrapper")
                {
                    mustQueries.Add(m => m.Wrapper(w => w.Query(query.Value)));
                }
            }

            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Knn(k => k
                    .Field(DataSourceConstants.ColumnNames.Embedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10)
                    .Filter(f => f.Bool(b => b.Must(mustQueries.ToArray()))))
                .Size(topN),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch data source vector search failed: {Error}", response.DebugInformation);

                return [];
            }

            var results = new List<DataSourceSearchResult>();
            var documents = response.Documents.GetEnumerator();
            var hits = response.Hits.GetEnumerator();

            while (documents.MoveNext() && hits.MoveNext())
            {
                var document = documents.Current;
                var hit = hits.Current;

                if (document == null)
                {
                    continue;
                }

                var content = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.Content, out var contentNode)
                    ? contentNode?.GetValue<string>()
                    : null;

                if (!string.IsNullOrEmpty(content))
                {
                    results.Add(new DataSourceSearchResult
                    {
                        ReferenceId = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ReferenceId, out var referenceIdNode) ? referenceIdNode?.GetValue<string>() : null,
                        Title = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.Title, out var titleNode) ? titleNode?.GetValue<string>() : null,
                        Content = content,
                        ChunkIndex = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunkIndex, out var chunkIndexNode) && chunkIndexNode != null ? chunkIndexNode.GetValue<int>() : 0,
                        ReferenceType = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ReferenceType, out var referenceTypeNode) ? referenceTypeNode?.GetValue<string>() : null,
                        Score = (float)(hit.Score ?? 0.0),
                    });
                }
            }

            return results
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing data source vector search in Elasticsearch index '{IndexName}'.", indexProfile.IndexFullName);

            return [];
        }
    }

    public async Task<long> DeleteByDataSourceIdAsync(
        IIndexProfileInfo indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);

        try
        {
            var response = await _elasticClient.DeleteByQueryAsync<JsonObject>(
                indexProfile.IndexFullName,
                d => d.Query(q => q.Term(t => t.Field(DataSourceConstants.ColumnNames.DataSourceId).Value(dataSourceId))),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch delete by data source ID failed for index '{IndexName}': {Error}", indexProfile.IndexFullName, response.DebugInformation);

                return 0;
            }

            return response.Deleted ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting documents by data source ID '{DataSourceId}' from Elasticsearch index '{IndexName}'.", dataSourceId, indexProfile.IndexFullName);

            return 0;
        }
    }
}
