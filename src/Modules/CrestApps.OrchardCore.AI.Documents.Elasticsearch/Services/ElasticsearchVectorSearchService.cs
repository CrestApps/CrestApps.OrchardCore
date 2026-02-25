using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Elasticsearch.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="IVectorSearchService"/> for searching document embeddings.
/// Uses k-NN (k-Nearest Neighbors) search on flat document chunks with vector similarity.
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
        string referenceId,
        string referenceType,
        int topN,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceType);

        if (embedding.Length == 0)
        {
            return [];
        }

        try
        {
            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Knn(k => k
                    .Field(AIConstants.ColumnNames.Embedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10)
                    .Filter(f => f
                        .Bool(b => b
                            .Must(
                                m => m.Term(t => t
                                    .Field(AIConstants.ColumnNames.ReferenceId)
                                    .Value(referenceId)
                                ),
                                m => m.Term(t => t
                                    .Field(AIConstants.ColumnNames.ReferenceType)
                                    .Value(referenceType)
                                )
                            )
                        )
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

                var chunkText = document.TryGetPropertyValue(AIConstants.ColumnNames.Content, out var textNode)
                    ? textNode?.GetValue<string>()
                    : null;

                var chunkIndex = 0;
                if (document.TryGetPropertyValue(AIConstants.ColumnNames.ChunkIndex, out var indexNode) && indexNode != null)
                {
                    chunkIndex = indexNode.GetValue<int>();
                }

                if (!string.IsNullOrEmpty(chunkText))
                {
                    var documentKey = document.TryGetPropertyValue(AIConstants.ColumnNames.DocumentId, out var docIdNode)
                        ? docIdNode?.GetValue<string>()
                        : null;

                    var fileName = document.TryGetPropertyValue(AIConstants.ColumnNames.FileName, out var fileNameNode)
                        ? fileNameNode?.GetValue<string>()
                        : null;

                    results.Add(new DocumentChunkSearchResult
                    {
                        Chunk = new ChatInteractionDocumentChunk
                        {
                            Text = chunkText,
                            Index = chunkIndex,
                        },
                        DocumentKey = documentKey,
                        FileName = fileName,
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
            _logger.LogError(ex, "Error performing vector search in Elasticsearch index '{IndexName}'", indexProfile.IndexFullName);

            return [];
        }
    }
}
