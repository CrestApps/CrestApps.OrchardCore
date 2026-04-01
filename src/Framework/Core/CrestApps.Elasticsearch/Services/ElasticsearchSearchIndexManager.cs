using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;

namespace CrestApps.Elasticsearch.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="ISearchIndexManager"/>
/// for creating, deleting, and checking search indexes.
/// </summary>
internal sealed class ElasticsearchSearchIndexManager : ISearchIndexManager
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<ElasticsearchSearchIndexManager> _logger;

    public ElasticsearchSearchIndexManager(
        ElasticsearchClient elasticClient,
        ILogger<ElasticsearchSearchIndexManager> logger)
    {
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(string indexFullName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName);

        try
        {
            var response = await _elasticClient.Indices.ExistsAsync(indexFullName, cancellationToken);

            return response.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of Elasticsearch index '{IndexName}'.", indexFullName);

            return false;
        }
    }

    public async Task CreateAsync(
        IIndexProfileInfo profile,
        IReadOnlyCollection<SearchIndexField> fields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(fields);

        try
        {
            var properties = new Properties();

            foreach (var field in fields)
            {
                properties[field.Name] = field.FieldType switch
                {
                    SearchFieldType.Vector => new DenseVectorProperty
                    {
                        Dims = field.VectorDimensions ?? 1536,
                        Index = true,
                        Similarity = DenseVectorSimilarity.Cosine,
                    },
                    SearchFieldType.Text => new TextProperty(),
                    SearchFieldType.Integer => new IntegerNumberProperty(),
                    SearchFieldType.Float => new FloatNumberProperty(),
                    SearchFieldType.DateTime => new DateProperty(),
                    _ => new KeywordProperty(),
                };
            }

            var response = await _elasticClient.Indices.CreateAsync(profile.IndexFullName, c => c
                .Mappings(m => m
                .Properties(properties)
            ),
            cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Failed to create Elasticsearch index '{IndexName}': {Error}",
                profile.IndexFullName, response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Elasticsearch index '{IndexName}'.", profile.IndexFullName);

            throw;
        }
    }

    public async Task DeleteAsync(string indexFullName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName);

        try
        {
            var response = await _elasticClient.Indices.DeleteAsync(indexFullName, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Failed to delete Elasticsearch index '{IndexName}': {Error}",
                indexFullName, response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Elasticsearch index '{IndexName}'.", indexFullName);

            throw;
        }
    }
}
