using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.AzureAI.Services;

namespace CrestApps.OrchardCore.AI.Memory.AzureAI.Services;

/// <summary>
/// Provides azure AI search memory vector search services.
/// </summary>
public sealed class AzureAISearchMemoryVectorSearchService : IMemoryVectorSearchService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAISearchMemoryVectorSearchService"/> class.
    /// </summary>
    /// <param name="clientFactory">The client factory.</param>
    /// <param name="logger">The logger.</param>
    public AzureAISearchMemoryVectorSearchService(
        AzureAIClientFactory clientFactory,
        ILogger<AzureAISearchMemoryVectorSearchService> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Searches for async.
    /// </summary>
    /// <param name="indexProfile">The index profile.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="topN">The top n.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
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
            var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);
            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = topN,
                Fields =
                {
                    MemoryConstants.ColumnNames.Embedding,
                },
            };

            var options = new SearchOptions
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

            var response = await searchClient.SearchAsync<SearchDocument>(null, options, cancellationToken);
            var results = new List<AIMemorySearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;
                var updatedUtc = document.TryGetValue(MemoryConstants.ColumnNames.UpdatedUtc, out var updatedObj) &&
                    DateTime.TryParse(updatedObj?.ToString(), out var parsedUpdatedUtc)
                ? parsedUpdatedUtc
                : null as DateTime?;

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
