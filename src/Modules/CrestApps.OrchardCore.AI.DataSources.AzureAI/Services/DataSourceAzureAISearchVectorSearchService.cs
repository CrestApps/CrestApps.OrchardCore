using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="IDataSourceVectorSearchService"/>
/// for searching data source embedding indexes using vector similarity.
/// </summary>
public sealed class DataSourceAzureAISearchVectorSearchService : IDataSourceVectorSearchService
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly ILogger _logger;

    public DataSourceAzureAISearchVectorSearchService(
        SearchIndexClient searchIndexClient,
        ILogger<DataSourceAzureAISearchVectorSearchService> logger)
    {
        _searchIndexClient = searchIndexClient;
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
            var searchClient = _searchIndexClient.GetSearchClient(indexProfile.IndexFullName);

            var vectorQuery = new VectorizedQuery(embedding)
            {
                KNearestNeighborsCount = topN,
                Fields =
                {
                    DataSourceConstants.ColumnNames.ChunksEmbedding,
                }
            };

            // Build filter expression.
            var filter = $"{DataSourceConstants.ColumnNames.DataSourceId} eq '{dataSourceId}'";

            var referenceIdList = referenceIds?.ToList();
            if (referenceIdList is { Count: > 0 })
            {
                var refFilter = string.Join(" or ", referenceIdList.Select(id =>
                    $"{DataSourceConstants.ColumnNames.ReferenceId} eq '{id}'"));
                filter = $"({filter}) and ({refFilter})";
            }

            var searchOptions = new SearchOptions
            {
                Filter = filter,
                Size = topN,
                Select =
                {
                    DataSourceConstants.ColumnNames.ReferenceId,
                    DataSourceConstants.ColumnNames.DataSourceId,
                    DataSourceConstants.ColumnNames.Title,
                    DataSourceConstants.ColumnNames.Chunks,
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

            var results = new List<DataSourceSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;

                var referenceId = document.TryGetValue(DataSourceConstants.ColumnNames.ReferenceId, out var refObj)
                    ? refObj?.ToString()
                    : null;

                var title = document.TryGetValue(DataSourceConstants.ColumnNames.Title, out var titleObj)
                    ? titleObj?.ToString()
                    : null;

                if (document.TryGetValue(DataSourceConstants.ColumnNames.Chunks, out var chunksObj) &&
                    chunksObj is IEnumerable<object> chunks)
                {
                    foreach (var chunkObj in chunks)
                    {
                        if (chunkObj is not IDictionary<string, object> chunk)
                        {
                            continue;
                        }

                        var chunkText = chunk.TryGetValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Text, out var textObj)
                            ? textObj?.ToString()
                            : null;

                        var chunkIndex = 0;
                        if (chunk.TryGetValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Index, out var indexObj))
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
                            results.Add(new DataSourceSearchResult
                            {
                                ReferenceId = referenceId,
                                Title = title,
                                Text = chunkText,
                                ChunkIndex = chunkIndex,
                                Score = (float)(result.Score ?? 0.0)
                            });
                        }
                    }
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
            _logger.LogError(ex, "Error performing data source vector search in Azure AI Search index '{IndexName}'",
                indexProfile.IndexFullName);

            return [];
        }
    }
}
