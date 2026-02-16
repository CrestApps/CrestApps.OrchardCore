using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
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
        IEnumerable<string> referenceIds = null,
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
            var mustQueries = new List<Action<QueryDescriptor<JsonObject>>>
            {
                m => m.Term(t => t
                    .Field(DataSourceConstants.ColumnNames.DataSourceId)
                    .Value(dataSourceId)
                ),
            };

            // If reference IDs are provided (from two-phase filter search),
            // constrain the vector search to only those documents.
            var referenceIdList = referenceIds?.ToList();
            if (referenceIdList is { Count: > 0 })
            {
                mustQueries.Add(m => m.Terms(t => t
                    .Field(DataSourceConstants.ColumnNames.ReferenceId)
                    .Terms(new TermsQueryField(referenceIdList.Select(id => FieldValue.String(id)).ToArray()))
                ));
            }

            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Query(q => q
                    .Bool(b => b
                        .Must(mustQueries.ToArray())
                    )
                )
                .Knn(k => k
                    .Field(DataSourceConstants.ColumnNames.ChunksEmbedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10)
                    .InnerHits(ih => ih
                        .Size(topN)
                        .Source(true)
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

                if (hit.InnerHits != null && hit.InnerHits.TryGetValue(DataSourceConstants.ColumnNames.Chunks, out var innerHits))
                {
                    foreach (var innerHit in innerHits.Hits.Hits)
                    {
                        var chunkResult = ExtractChunkFromInnerHit(innerHit, referenceId, title, hit.Score);
                        if (chunkResult != null)
                        {
                            results.Add(chunkResult);
                        }
                    }
                }
                else
                {
                    var chunkResults = ExtractChunksFromSource(document, referenceId, title, hit.Score);
                    results.AddRange(chunkResults);
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

    private static DataSourceSearchResult ExtractChunkFromInnerHit(Hit<object> innerHit, string referenceId, string title, double? parentScore)
    {
        try
        {
            if (innerHit.Source == null)
            {
                return null;
            }

            var sourceJson = JsonSerializer.Serialize(innerHit.Source);
            var chunkSource = JsonSerializer.Deserialize<JsonObject>(sourceJson);

            if (chunkSource == null)
            {
                return null;
            }

            var chunkText = chunkSource.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Text, out var textNode)
                ? textNode?.GetValue<string>()
                : null;

            var chunkIndex = 0;
            if (chunkSource.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Index, out var indexNode) && indexNode != null)
            {
                chunkIndex = indexNode.GetValue<int>();
            }

            if (string.IsNullOrEmpty(chunkText))
            {
                return null;
            }

            return new DataSourceSearchResult
            {
                ReferenceId = referenceId,
                Title = title,
                Text = chunkText,
                ChunkIndex = chunkIndex,
                Score = (float)(innerHit.Score ?? parentScore ?? 0.0),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<DataSourceSearchResult> ExtractChunksFromSource(
        JsonObject source,
        string referenceId,
        string title,
        double? score)
    {
        var results = new List<DataSourceSearchResult>();

        if (!source.TryGetPropertyValue(DataSourceConstants.ColumnNames.Chunks, out var chunksNode) || chunksNode is not JsonArray chunks)
        {
            return results;
        }

        foreach (var chunk in chunks)
        {
            if (chunk is not JsonObject chunkDict)
            {
                continue;
            }

            var chunkText = chunkDict.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Text, out var textNode)
                ? textNode?.GetValue<string>()
                : null;

            var chunkIndex = 0;

            if (chunkDict.TryGetPropertyValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Index, out var indexNode) && indexNode != null)
            {
                chunkIndex = indexNode.GetValue<int>();
            }

            if (!string.IsNullOrEmpty(chunkText))
            {
                results.Add(new DataSourceSearchResult
                {
                    ReferenceId = referenceId,
                    Title = title,
                    Text = chunkText,
                    ChunkIndex = chunkIndex,
                    Score = (float)(score ?? 0.0),
                });
            }
        }

        return results;
    }
}
