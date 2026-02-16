using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Executes Elasticsearch DSL filter queries against a source index
/// and returns matching document keys for two-phase RAG search.
/// </summary>
internal sealed class DataSourceElasticsearchFilterExecutor : IDataSourceFilterExecutor
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<DataSourceElasticsearchFilterExecutor> _logger;

    public DataSourceElasticsearchFilterExecutor(
        ElasticsearchClient elasticClient,
        ILogger<DataSourceElasticsearchFilterExecutor> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> ExecuteAsync(
        string indexName,
        string filter,
        CancellationToken cancellationToken = default)
    {
        // Parse and validate the filter query JSON.
        try
        {
            JsonNode.Parse(filter)?.AsObject();
        }
        catch (JsonException)
        {
            _logger.LogWarning("Invalid Elasticsearch DSL filter query JSON provided.");
            return null;
        }

        try
        {
            var filterBytes = Encoding.UTF8.GetBytes(filter);
            var filterBase64 = Convert.ToBase64String(filterBytes);

            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexName)
                .Source(false)
                .Size(10000)
                .Query(q => q.Wrapper(w => w.Query(filterBase64)))
            , cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch filter query failed: {Error}", response.DebugInformation);
                return null;
            }

            return response.Hits
                .Where(h => h.Id != null)
                .Select(h => h.Id)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Elasticsearch filter query against index '{IndexName}'.", indexName);
            return null;
        }
    }
}
