using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.Logging;
using AzureSearchDocument = Azure.Search.Documents.Models.SearchDocument;

namespace CrestApps.Core.Azure.AISearch.Services;

internal sealed class AzureAISearchMemoryVectorSearchService : IMemoryVectorSearchService
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly ILogger<AzureAISearchMemoryVectorSearchService> _logger;

    public AzureAISearchMemoryVectorSearchService(
        SearchIndexClient searchIndexClient,
        ILogger<AzureAISearchMemoryVectorSearchService> logger)
    {
        _searchIndexClient = searchIndexClient;
        _logger = logger;
    }

    public async Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        SearchIndexProfile indexProfile,
        float[] embedding,
        string userId,
        int topN,
        CancellationToken cancellationToken = default)
    {
        if (embedding is null || embedding.Length == 0 || string.IsNullOrWhiteSpace(userId))
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
                    MemoryConstants.ColumnNames.Embedding,
                },
            };

            var searchOptions = new SearchOptions
            {
                Filter = $"{MemoryConstants.ColumnNames.UserId} eq '{userId}'",
                Size = topN,
                Select =
                {
                    MemoryConstants.ColumnNames.MemoryId,
                    MemoryConstants.ColumnNames.Name,
                    MemoryConstants.ColumnNames.Description,
                    MemoryConstants.ColumnNames.Content,
                    MemoryConstants.ColumnNames.UpdatedUtc,
                },
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { vectorQuery },
                },
            };

            var response = await searchClient.SearchAsync<AzureSearchDocument>(null, searchOptions, cancellationToken);
            var results = new List<AIMemorySearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;
                DateTime? updatedUtc = null;

                if (document.TryGetValue(MemoryConstants.ColumnNames.UpdatedUtc, out var updatedObj) &&
                    DateTime.TryParse(updatedObj?.ToString(), out var parsedUpdatedUtc))
                {
                    updatedUtc = parsedUpdatedUtc;
                }

                results.Add(new AIMemorySearchResult
                {
                    MemoryId = document.TryGetValue(MemoryConstants.ColumnNames.MemoryId, out var idObj) ? idObj?.ToString() : null,
                    Name = document.TryGetValue(MemoryConstants.ColumnNames.Name, out var nameObj) ? nameObj?.ToString() : null,
                    Description = document.TryGetValue(MemoryConstants.ColumnNames.Description, out var descriptionObj) ? descriptionObj?.ToString() : null,
                    Content = document.TryGetValue(MemoryConstants.ColumnNames.Content, out var contentObj) ? contentObj?.ToString() : null,
                    UpdatedUtc = updatedUtc,
                    Score = (float)(result.Score ?? 0.0),
                });
            }

            return results
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToArray();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure AI Search request failed for AI memory index '{IndexName}'.", indexProfile.IndexFullName);

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching AI memory index '{IndexName}'.", indexProfile.IndexFullName);

            return [];
        }
    }
}
