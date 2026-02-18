using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="IVectorSearchService"/> for searching document embeddings.
/// Uses vector search on flat document chunks with similarity scoring.
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
            var searchClient = _searchIndexClient.GetSearchClient(indexProfile.IndexFullName);

            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = topN,
                Fields =
                {
                    AIConstants.ColumnNames.Embedding,
                }
            };

            var searchOptions = new SearchOptions
            {
                Filter = $"{AIConstants.ColumnNames.ReferenceId} eq '{referenceId}' and {AIConstants.ColumnNames.ReferenceType} eq '{referenceType}'",
                Size = topN,
                Select =
                {
                    AIConstants.ColumnNames.ChunkId,
                    AIConstants.ColumnNames.DocumentId,
                    AIConstants.ColumnNames.Content,
                    AIConstants.ColumnNames.FileName,
                    AIConstants.ColumnNames.ChunkIndex,
                },
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { vectorQuery },
                }
            };

            var response = await searchClient.SearchAsync<SearchDocument>(
                searchText: null,
                searchOptions,
                cancellationToken);

            var results = new List<DocumentChunkSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;

                var chunkText = document.TryGetValue(AIConstants.ColumnNames.Content, out var textObj)
                    ? textObj?.ToString()
                    : null;

                var chunkIndex = 0;
                if (document.TryGetValue(AIConstants.ColumnNames.ChunkIndex, out var indexObj))
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
                        Score = (float)(result.Score ?? 0.0),
                    });
                }
            }

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
