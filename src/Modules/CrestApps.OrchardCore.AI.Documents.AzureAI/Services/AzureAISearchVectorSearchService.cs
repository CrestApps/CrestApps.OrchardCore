using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.OrchardCore.AI.Core.Search;
using Microsoft.Extensions.Logging;
using OrchardCore.AzureAI.Services;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="IVectorSearchService"/> for searching document embeddings.
/// Uses vector search on flat document chunks with similarity scoring.
/// </summary>
public sealed class AzureAISearchVectorSearchService : IVectorSearchService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchVectorSearchService"/> class.
    /// </summary>
    /// <param name="clientFactory">The client factory.</param>
    /// <param name="logger">The logger.</param>
    public AzureAISearchVectorSearchService(
        AzureAIClientFactory clientFactory,
        ILogger<AzureAISearchVectorSearchService> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Builds the filter that limits the search to one reference, with both values escaped.
    /// </summary>
    /// <param name="referenceId">The reference identifier.</param>
    /// <param name="referenceType">The reference type.</param>
    internal static string BuildFilter(string referenceId, string referenceType)
        => $"{ODataFilter.Equal(DocumentIndexConstants.ColumnNames.ReferenceId, referenceId)} and {ODataFilter.Equal(DocumentIndexConstants.ColumnNames.ReferenceType, referenceType)}";

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
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
            var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);

            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = topN,
                Fields =
                {
                    DocumentIndexConstants.ColumnNames.Embedding,
                }
            };

            var searchOptions = new SearchOptions
            {
                Filter = BuildFilter(referenceId, referenceType),
                Size = topN,
                Select =
                {
                    DocumentIndexConstants.ColumnNames.ChunkId,
                    DocumentIndexConstants.ColumnNames.DocumentId,
                    DocumentIndexConstants.ColumnNames.Content,
                    DocumentIndexConstants.ColumnNames.FileName,
                    DocumentIndexConstants.ColumnNames.ChunkIndex,
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

                var chunkText = document.TryGetValue(DocumentIndexConstants.ColumnNames.Content, out var textObj)
                ? textObj?.ToString()
                : null;

                var chunkIndex = 0;

                if (document.TryGetValue(DocumentIndexConstants.ColumnNames.ChunkIndex, out var indexObj))
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
                    var documentKey = document.TryGetValue(DocumentIndexConstants.ColumnNames.DocumentId, out var docIdObj)
                    ? docIdObj?.ToString()
                    : null;

                    var fileName = document.TryGetValue(DocumentIndexConstants.ColumnNames.FileName, out var fileNameObj)
                    ? fileNameObj?.ToString()
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
