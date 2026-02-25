using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="IDataSourceVectorSearchService"/>
/// for searching data source embedding indexes using k-NN vector similarity.
/// </summary>
internal sealed class DataSourceElasticsearchVectorSearchService : IDataSourceVectorSearchService
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<DataSourceElasticsearchVectorSearchService> _logger;

    internal static List<(string Kind, string Value)> BuildMustQueryDebug(string dataSourceId, string filter)
    {
        var list = new List<(string Kind, string Value)>
        {
            ("term", dataSourceId),
        };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterBytes = System.Text.Encoding.UTF8.GetBytes(filter);
            var filterBase64 = Convert.ToBase64String(filterBytes);
            list.Add(("wrapper", filterBase64));
        }

        return list;
    }

    public DataSourceElasticsearchVectorSearchService(
        ElasticsearchClient elasticClient,
        ILogger<DataSourceElasticsearchVectorSearchService> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IndexProfile indexProfile,
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

            foreach (var item in BuildMustQueryDebug(dataSourceId, filter))
            {
                if (item.Kind == "term")
                {
                    mustQueries.Add(m => m.Term(t => t
                        .Field(DataSourceConstants.ColumnNames.DataSourceId)
                        .Value(item.Value)
                    ));
                }
                else if (item.Kind == "wrapper")
                {
                    mustQueries.Add(m => m.Wrapper(w => w.Query(item.Value)));
                }
            }

            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Knn(k => k
                    .Field(DataSourceConstants.ColumnNames.Embedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10)
                    .Filter(f => f
                        .Bool(b => b
                            .Must(mustQueries.ToArray())
                        )
                    )
                )
                .Size(topN)
            , cancellationToken);

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
                var hit = hits.Current;
                var document = documents.Current;

                if (document == null)
                {
                    continue;
                }

                var referenceId = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ReferenceId, out var refNode)
                    ? refNode?.GetValue<string>()
                    : null;

                var title = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.Title, out var titleNode)
                    ? titleNode?.GetValue<string>()
                    : null;

                var content = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.Content, out var contentNode)
                    ? contentNode?.GetValue<string>()
                    : null;

                var chunkIndex = 0;
                if (document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunkIndex, out var chunkIndexNode) && chunkIndexNode != null)
                {
                    chunkIndex = chunkIndexNode.GetValue<int>();
                }

                var referenceType = document.TryGetPropertyValue(DataSourceConstants.ColumnNames.ReferenceType, out var refTypeNode)
                    ? refTypeNode?.GetValue<string>()
                    : null;

                if (!string.IsNullOrEmpty(content))
                {
                    results.Add(new DataSourceSearchResult
                    {
                        ReferenceId = referenceId,
                        Title = title,
                        Content = content,
                        ChunkIndex = chunkIndex,
                        ReferenceType = referenceType,
                        Score = (float)(hit.Score ?? 0.0),
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing data source vector search in Elasticsearch index '{IndexName}'", indexProfile.IndexFullName);

            return [];
        }
    }
}
