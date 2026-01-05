using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="IVectorSearchService"/> for searching document embeddings.
/// Uses k-NN (k-Nearest Neighbors) search on nested document chunks with vector similarity.
/// </summary>
public sealed class ElasticsearchVectorSearchService : IVectorSearchService
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<ElasticsearchVectorSearchService> _logger;

    public ElasticsearchVectorSearchService(
        ElasticsearchClient elasticClient,
        ILogger<ElasticsearchVectorSearchService> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string sessionId,
        int topN,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (embedding.Length == 0)
        {
            return [];
        }

        try
        {
            // Build a nested k-NN query to search for similar document chunks
            // The documents are stored with nested "chunks" that contain the embedding vectors
            var response = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Indices(indexProfile.IndexFullName)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            // Filter by interaction/session ID
                            m => m.Term(t => t
                                .Field(ChatInteractionsConstants.ColumnNames.InteractionId)
                                .Value(sessionId)
                            )
                        )
                    )
                )
                .Knn(k => k
                    .Field(ChatInteractionsConstants.ColumnNames.ChunksEmbedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10) // Search more candidates for better accuracy
                    .InnerHits(ih => ih
                        .Size(topN)
                        .Source(false)
                    )
                )
                .Size(topN)
            , cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch vector search failed: {Error}", response.DebugInformation);

                return [];
            }

            var results = new List<DocumentChunkSearchResult>();

            foreach (var hit in response.Hits)
            {
                if (hit.Source == null)
                {
                    continue;
                }

                // Process inner hits from the nested chunks
                if (hit.InnerHits != null && hit.InnerHits.TryGetValue(ChatInteractionsConstants.ColumnNames.Chunks, out var innerHits))
                {
                    foreach (var innerHit in innerHits.Hits.Hits)
                    {
                        var chunkResult = ExtractChunkFromInnerHit(innerHit, hit.Score);
                        if (chunkResult != null)
                        {
                            results.Add(chunkResult);
                        }
                    }
                }
                else
                {
                    // Fallback: try to extract chunks directly from the source
                    var chunkResults = ExtractChunksFromSource(hit.Source, hit.Score);
                    results.AddRange(chunkResults);
                }
            }

            // Return top N results sorted by score
            return results
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing vector search in Elasticsearch index '{IndexName}'", indexProfile.IndexFullName);

            return [];
        }
    }

    private static DocumentChunkSearchResult ExtractChunkFromInnerHit(Hit<object> innerHit, double? parentScore)
    {
        try
        {
            // Get the chunk text from the nested hit
            string chunkText = null;
            var chunkIndex = 0;

            if (innerHit.Source is Dictionary<string, object> chunkSource)
            {
                chunkText = chunkSource.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, out var text)
                    ? text?.ToString()
                    : null;

                if (chunkSource.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, out var idx) && idx != null)
                {
                    _ = int.TryParse(idx.ToString(), out chunkIndex);
                }
            }

            if (string.IsNullOrEmpty(chunkText))
            {
                return null;
            }

            return new DocumentChunkSearchResult
            {
                Chunk = new DocumentChunk
                {
                    Text = chunkText,
                    Index = chunkIndex
                },
                Score = (float)(innerHit.Score ?? parentScore ?? 0.0)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<DocumentChunkSearchResult> ExtractChunksFromSource(
        Dictionary<string, object> source,
        double? score)
    {
        var results = new List<DocumentChunkSearchResult>();

        if (!source.TryGetValue(ChatInteractionsConstants.ColumnNames.Chunks, out var chunksObj) || chunksObj is not IEnumerable<object> chunks)
        {
            return results;
        }

        foreach (var chunk in chunks)
        {
            if (chunk is not Dictionary<string, object> chunkDict)
            {
                continue;
            }

            var chunkText = chunkDict.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, out var text)
                ? text?.ToString()
                : null;

            var chunkIndex = 0;

            if (chunkDict.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, out var idx) && idx != null)
            {
                _ = int.TryParse(idx.ToString(), out chunkIndex);
            }

            if (!string.IsNullOrEmpty(chunkText))
            {
                results.Add(new DocumentChunkSearchResult
                {
                    Chunk = new DocumentChunk
                    {
                        Text = chunkText,
                        Index = chunkIndex
                    },
                    Score = (float)(score ?? 0.0)
                });
            }
        }

        return results;
    }
}
