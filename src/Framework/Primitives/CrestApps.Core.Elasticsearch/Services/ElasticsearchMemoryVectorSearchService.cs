using System.Text.Json.Nodes;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Elasticsearch.Services;

internal sealed class ElasticsearchMemoryVectorSearchService : IMemoryVectorSearchService
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<ElasticsearchMemoryVectorSearchService> _logger;

    public ElasticsearchMemoryVectorSearchService(
        ElasticsearchClient elasticClient,
        ILogger<ElasticsearchMemoryVectorSearchService> logger)
    {
        _elasticClient = elasticClient;
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
            var response = await _elasticClient.SearchAsync<JsonObject>(s => s
                .Indices(indexProfile.IndexFullName)
                .Knn(k => k
                    .Field(MemoryConstants.ColumnNames.Embedding)
                    .QueryVector(embedding)
                    .K(topN)
                    .NumCandidates(topN * 10)
                    .Filter(f => f.Term(t => t
                        .Field(MemoryConstants.ColumnNames.UserId)
                        .Value(userId))))
                .Size(topN),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch AI memory search failed: {Error}", response.DebugInformation);

                return [];
            }

            var results = new List<AIMemorySearchResult>();
            var documents = response.Documents.GetEnumerator();
            var hits = response.Hits.GetEnumerator();

            while (documents.MoveNext() && hits.MoveNext())
            {
                var document = documents.Current;
                var hit = hits.Current;

                if (document is null)
                {
                    continue;
                }

                DateTime? updatedUtc = null;

                if (document.TryGetPropertyValue(MemoryConstants.ColumnNames.UpdatedUtc, out var updatedNode) &&
                    DateTime.TryParse(updatedNode?.ToString(), out var parsedUpdatedUtc))
                {
                    updatedUtc = parsedUpdatedUtc;
                }

                results.Add(new AIMemorySearchResult
                {
                    MemoryId = document.TryGetPropertyValue(MemoryConstants.ColumnNames.MemoryId, out var idNode) ? idNode?.GetValue<string>() : null,
                    Name = document.TryGetPropertyValue(MemoryConstants.ColumnNames.Name, out var nameNode) ? nameNode?.GetValue<string>() : null,
                    Description = document.TryGetPropertyValue(MemoryConstants.ColumnNames.Description, out var descriptionNode) ? descriptionNode?.GetValue<string>() : null,
                    Content = document.TryGetPropertyValue(MemoryConstants.ColumnNames.Content, out var contentNode) ? contentNode?.GetValue<string>() : null,
                    UpdatedUtc = updatedUtc,
                    Score = (float)(hit.Score ?? 0.0),
                });
            }

            return results
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing AI memory search in Elasticsearch index '{IndexName}'.", indexProfile.IndexFullName);

            return [];
        }
    }
}
