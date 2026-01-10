using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;

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
        string interactionId,
        int topN,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionId);

        if (embedding.Length == 0)
        {
            return [];
        }

        try
        {
            // Build a nested k-NN query to search for similar document chunks
            // The documents are stored with nested "chunks" that contain the embedding vectors
            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            // Filter by interaction/session ID
                            m => m.Term(t => t
                                .Field(ChatInteractionsConstants.ColumnNames.InteractionId)
                                .Value(interactionId)
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
                        .Source(true) // Include source so we can access chunk content
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

            // Iterate over documents and hits together as per OrchardCore pattern
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
                    var chunkResults = ExtractChunksFromSource(document, hit.Score);
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
            if (innerHit.Source == null)
            {
                return null;
            }

            // The Source is an object, we need to serialize and deserialize it to access properties
            var sourceJson = JsonSerializer.Serialize(innerHit.Source);
            var chunkSource = JsonSerializer.Deserialize<JsonObject>(sourceJson);

            if (chunkSource == null)
            {
                return null;
            }

            // Get the chunk text from the nested hit
            var chunkText = chunkSource.TryGetPropertyValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, out var textNode)
                ? textNode?.GetValue<string>()
                : null;

            var chunkIndex = 0;
            if (chunkSource.TryGetPropertyValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, out var indexNode) && indexNode != null)
            {
                chunkIndex = indexNode.GetValue<int>();
            }

            if (string.IsNullOrEmpty(chunkText))
            {
                return null;
            }

            return new DocumentChunkSearchResult
            {
                Chunk = new ChatInteractionDocumentChunk
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
        JsonObject source,
        double? score)
    {
        var results = new List<DocumentChunkSearchResult>();

        if (!source.TryGetPropertyValue(ChatInteractionsConstants.ColumnNames.Chunks, out var chunksNode) || chunksNode is not JsonArray chunks)
        {
            return results;
        }

        foreach (var chunk in chunks)
        {
            if (chunk is not JsonObject chunkDict)
            {
                continue;
            }

            var chunkText = chunkDict.TryGetPropertyValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, out var textNode)
                ? textNode?.GetValue<string>()
                : null;

            var chunkIndex = 0;

            if (chunkDict.TryGetPropertyValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, out var indexNode) && indexNode != null)
            {
                chunkIndex = indexNode.GetValue<int>();
            }

            if (!string.IsNullOrEmpty(chunkText))
            {
                results.Add(new DocumentChunkSearchResult
                {
                    Chunk = new ChatInteractionDocumentChunk
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
