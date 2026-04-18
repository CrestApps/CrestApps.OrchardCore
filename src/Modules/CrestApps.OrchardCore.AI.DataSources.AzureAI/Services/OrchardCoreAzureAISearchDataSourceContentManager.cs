using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.AzureAI.Services;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

internal sealed class OrchardCoreAzureAISearchDataSourceContentManager : IDataSourceContentManager
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger _logger;

    internal static string BuildODataFilter(string dataSourceId, string filter)
    {
        var odataFilter = $"{DataSourceConstants.ColumnNames.DataSourceId} eq '{dataSourceId}'";

        if (!string.IsNullOrWhiteSpace(filter))
        {
            odataFilter = $"({odataFilter}) and ({filter})";
        }

        return odataFilter;
    }

    public OrchardCoreAzureAISearchDataSourceContentManager(
        AzureAIClientFactory clientFactory,
        ILogger<OrchardCoreAzureAISearchDataSourceContentManager> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IIndexProfileInfo indexProfile,
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
            var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);
            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = topN,
                Fields =
                {
                    DataSourceConstants.ColumnNames.Embedding,
                },
            };

            var searchOptions = new SearchOptions
            {
                Filter = BuildODataFilter(dataSourceId, filter),
                Size = topN,
                Select =
                {
                    DataSourceConstants.ColumnNames.ChunkId,
                    DataSourceConstants.ColumnNames.ReferenceId,
                    DataSourceConstants.ColumnNames.DataSourceId,
                    DataSourceConstants.ColumnNames.ReferenceType,
                    DataSourceConstants.ColumnNames.Title,
                    DataSourceConstants.ColumnNames.Content,
                    DataSourceConstants.ColumnNames.ChunkIndex,
                },
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { vectorQuery },
                },
            };

            var response = await searchClient.SearchAsync<SearchDocument>(null, searchOptions, cancellationToken);
            var results = new List<DataSourceSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;
                var chunkIndex = 0;

                if (document.TryGetValue(DataSourceConstants.ColumnNames.ChunkIndex, out var chunkIndexObj))
                {
                    if (chunkIndexObj is int intValue)
                    {
                        chunkIndex = intValue;
                    }
                    else if (int.TryParse(chunkIndexObj?.ToString(), out var parsedIndex))
                    {
                        chunkIndex = parsedIndex;
                    }
                }

                var content = document.TryGetValue(DataSourceConstants.ColumnNames.Content, out var contentObj)
                    ? contentObj?.ToString()
                    : null;

                if (!string.IsNullOrEmpty(content))
                {
                    results.Add(new DataSourceSearchResult
                    {
                        ReferenceId = document.TryGetValue(DataSourceConstants.ColumnNames.ReferenceId, out var referenceIdObj) ? referenceIdObj?.ToString() : null,
                        Title = document.TryGetValue(DataSourceConstants.ColumnNames.Title, out var titleObj) ? titleObj?.ToString() : null,
                        Content = content,
                        ChunkIndex = chunkIndex,
                        ReferenceType = document.TryGetValue(DataSourceConstants.ColumnNames.ReferenceType, out var referenceTypeObj) ? referenceTypeObj?.ToString() : null,
                        Score = (float)(result.Score ?? 0.0),
                    });
                }
            }

            return results
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToArray();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure AI Search request failed for index '{IndexName}': {Message}", indexProfile.IndexFullName, ex.Message);

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing data source vector search in Azure AI Search index '{IndexName}'.", indexProfile.IndexFullName);

            return [];
        }
    }

    public async Task<long> DeleteByDataSourceIdAsync(
        IIndexProfileInfo indexProfile,
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);

        try
        {
            var searchClient = _clientFactory.CreateSearchClient(indexProfile.IndexFullName);
            var filter = $"{DataSourceConstants.ColumnNames.DataSourceId} eq '{dataSourceId}'";
            long totalDeleted = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await searchClient.SearchAsync<SearchDocument>(
                    "*",
                    new SearchOptions
                    {
                        Filter = filter,
                        Size = 1000,
                        Select = { DataSourceConstants.ColumnNames.ChunkId },
                    },
                    cancellationToken);

                var keysToDelete = new List<string>();

                await foreach (var result in response.Value.GetResultsAsync())
                {
                    if (result.Document.TryGetValue(DataSourceConstants.ColumnNames.ChunkId, out var chunkIdObj) &&
                        chunkIdObj?.ToString() is { Length: > 0 } chunkId)
                    {
                        keysToDelete.Add(chunkId);
                    }
                }

                if (keysToDelete.Count == 0)
                {
                    break;
                }

                var batch = IndexDocumentsBatch.Delete(DataSourceConstants.ColumnNames.ChunkId, keysToDelete);
                await searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
                totalDeleted += keysToDelete.Count;

                if (keysToDelete.Count < 1000)
                {
                    break;
                }
            }

            return totalDeleted;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure AI Search delete by data source ID failed for index '{IndexName}': {Message}", indexProfile.IndexFullName, ex.Message);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting documents by data source ID '{DataSourceId}' from Azure AI Search index '{IndexName}'.", dataSourceId, indexProfile.IndexFullName);

            return 0;
        }
    }
}
