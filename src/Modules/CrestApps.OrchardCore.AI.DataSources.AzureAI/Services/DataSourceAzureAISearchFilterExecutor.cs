using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Executes OData filter queries against an Azure AI Search source index
/// and returns matching document keys for two-phase RAG search.
/// </summary>
internal sealed class DataSourceAzureAISearchFilterExecutor : IDataSourceFilterExecutor
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly ILogger<DataSourceAzureAISearchFilterExecutor> _logger;

    public DataSourceAzureAISearchFilterExecutor(
        SearchIndexClient searchIndexClient,
        ILogger<DataSourceAzureAISearchFilterExecutor> logger)
    {
        _searchIndexClient = searchIndexClient;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> ExecuteAsync(
        string indexName,
        string filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _searchIndexClient.GetSearchClient(indexName);

            var searchOptions = new SearchOptions
            {
                Filter = filter,
                Size = 10000,
                Select = { },
            };

            var response = await searchClient.SearchAsync<SearchDocument>(
                searchText: "*",
                searchOptions,
                cancellationToken);

            var keys = new List<string>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var key = result.Document.Keys.FirstOrDefault();

                if (key != null && result.Document.TryGetValue(key, out var keyValue))
                {
                    var keyStr = keyValue?.ToString();

                    if (!string.IsNullOrEmpty(keyStr))
                    {
                        keys.Add(keyStr);
                    }
                }
            }

            return keys.Distinct().ToList();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Azure AI Search filter query failed. Ensure the filter is a valid OData expression.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Azure AI Search filter query against index '{IndexName}'.", indexName);
            return null;
        }
    }
}
