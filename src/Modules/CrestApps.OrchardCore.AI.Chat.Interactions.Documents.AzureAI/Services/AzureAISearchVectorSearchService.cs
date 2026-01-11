using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.AzureAI.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="IVectorSearchService"/> for searching document embeddings.
/// Uses vector search on nested document chunks with similarity scoring.
/// </summary>
public sealed class AzureAISearchVectorSearchService : IVectorSearchService
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly ILogger _logger;

    public AzureAISearchVectorSearchService(
        SearchIndexClient searchIndexClient,
        ILogger<AzureAISearchVectorSearchService> logger)
    {
        _searchIndexClient = searchIndexClient;
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
            var searchClient = _searchIndexClient.GetSearchClient(indexProfile.IndexFullName);

            // Build vector search options
            var vectorQuery = new VectorizedQuery(embedding)
            {
                // Target the nested embedding field within chunks
                KNearestNeighborsCount = topN,
                Fields =
                {
                    ChatInteractionsConstants.ColumnNames.ChunksEmbedding,
                }
            };

            var searchOptions = new SearchOptions
            {
                // Filter by interaction ID
                Filter = $"{ChatInteractionsConstants.ColumnNames.InteractionId} eq '{interactionId}'",
                Size = topN,
                // Include all fields in results so we can access chunk content
                Select =
                {
                    ChatInteractionsConstants.ColumnNames.DocumentId,
                    ChatInteractionsConstants.ColumnNames.InteractionId,
                    ChatInteractionsConstants.ColumnNames.FileName,
                    ChatInteractionsConstants.ColumnNames.Chunks,
                },
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { vectorQuery },
                }
            };

            var response = await searchClient.SearchAsync<SearchDocument>(
                searchText: null, // Vector-only search
                searchOptions,
                cancellationToken);

            var results = new List<DocumentChunkSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;

                // Extract chunks from the document
                if (document.TryGetValue(ChatInteractionsConstants.ColumnNames.Chunks, out var chunksObj) &&
                    chunksObj is IEnumerable<object> chunks)
                {
                    foreach (var chunkObj in chunks)
                    {
                        if (chunkObj is not IDictionary<string, object> chunk)
                        {
                            continue;
                        }

                        var chunkText = chunk.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, out var textObj)
                            ? textObj?.ToString()
                            : null;

                        var chunkIndex = 0;
                        if (chunk.TryGetValue(ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, out var indexObj))
                        {
                            if (indexObj is int intValue)
                            {
                                chunkIndex = intValue;
                            }
                            else if (int.TryParse(indexObj?.ToString(), out var parsedIndex))
                            {
                                chunkIndex = parsedIndex;
                            }
                        }

                        if (!string.IsNullOrEmpty(chunkText))
                        {
                            results.Add(new DocumentChunkSearchResult
                            {
                                Chunk = new ChatInteractionDocumentChunk
                                {
                                    Text = chunkText,
                                    Index = chunkIndex,
                                },
                                // Azure AI Search returns scores as double, convert to float
                                Score = (float)(result.Score ?? 0.0)
                            });
                        }
                    }
                }
            }

            // Return top N results sorted by score (highest first)
            return results
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure AI Search request failed for index '{IndexName}': {Message}",
                indexProfile.IndexFullName, ex.Message);

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing vector search in Azure AI Search index '{IndexName}'",
                indexProfile.IndexFullName);

            return [];
        }
    }
}
