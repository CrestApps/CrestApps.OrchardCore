using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="ISearchDocumentManager"/>
/// for adding, updating, and deleting documents in search indexes.
/// </summary>
internal sealed class ElasticsearchSearchDocumentManager : ISearchDocumentManager
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<ElasticsearchSearchDocumentManager> _logger;

    public ElasticsearchSearchDocumentManager(
        ElasticsearchClient elasticClient,
        ILogger<ElasticsearchSearchDocumentManager> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<bool> AddOrUpdateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<IndexDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return true;
        }

        try
        {
            var operations = new List<IBulkOperation>();

            foreach (var document in documents)
            {
                var jsonDoc = new JsonObject();

                foreach (var field in document.Fields)
                {
                    jsonDoc[field.Key] = JsonValue.Create(field.Value);
                }

                operations.Add(new BulkIndexOperation<JsonObject>(jsonDoc)
                {
                    Id = document.Id,
                    Index = profile.IndexFullName,
                });
            }

            var request = new BulkRequest
            {
                Operations = operations,
            };

            var response = await _elasticClient.BulkAsync(request, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch bulk index failed for index '{IndexName}': {Error}",
                    profile.IndexFullName, response.DebugInformation);

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing documents in Elasticsearch index '{IndexName}'.", profile.IndexFullName);

            return false;
        }
    }

    public async Task DeleteAsync(
        IIndexProfileInfo profile,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(documentIds);

        var ids = documentIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            var operations = new List<IBulkOperation>();

            foreach (var id in ids)
            {
                operations.Add(new BulkDeleteOperation(id)
                {
                    Index = profile.IndexFullName,
                });
            }

            var request = new BulkRequest
            {
                Operations = operations,
            };

            var response = await _elasticClient.BulkAsync(request, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch bulk delete failed for index '{IndexName}': {Error}",
                    profile.IndexFullName, response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting documents from Elasticsearch index '{IndexName}'.", profile.IndexFullName);
        }
    }

    public async Task DeleteAllAsync(
        IIndexProfileInfo profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        try
        {
            var response = await _elasticClient.DeleteByQueryAsync<JsonObject>(profile.IndexFullName, d => d
                .Query(q => q.MatchAll(m => { })),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch delete all failed for index '{IndexName}': {Error}",
                    profile.IndexFullName, response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all documents from Elasticsearch index '{IndexName}'.", profile.IndexFullName);
        }
    }
}
