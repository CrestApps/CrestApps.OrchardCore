using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Elasticsearch.Services;

/// <summary>
/// Elasticsearch implementation of <see cref="ISearchIndexManager"/>
/// for creating, deleting, and checking search indexes.
/// </summary>
internal sealed class ElasticsearchSearchIndexManager : ISearchIndexManager
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly ElasticsearchConnectionOptions _options;
    private readonly ILogger<ElasticsearchSearchIndexManager> _logger;

    public ElasticsearchSearchIndexManager(
        ElasticsearchClient elasticClient,
        IOptions<ElasticsearchConnectionOptions> options,
        ILogger<ElasticsearchSearchIndexManager> logger)
    {
        _elasticClient = elasticClient;
        _options = options.Value;
        _logger = logger;
    }

    private static string SanitizeLogValue(string value)
        => value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;

    public string ComposeIndexFullName(IIndexProfileInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var normalizedIndexName = profile.IndexName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIndexName))
        {
            return normalizedIndexName;
        }

        return string.IsNullOrWhiteSpace(_options.IndexPrefix)
            ? normalizedIndexName
            : string.Concat(_options.IndexPrefix.Trim(), normalizedIndexName);
    }

    public async Task<bool> ExistsAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var indexFullName = profile.IndexFullName ?? ComposeIndexFullName(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName);

        try
        {
            var response = await _elasticClient.Indices.ExistsAsync(indexFullName, cancellationToken);

            return response.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of Elasticsearch index '{IndexName}'.", SanitizeLogValue(indexFullName));

            throw;
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
                _logger.LogWarning("Failed to create Elasticsearch index '{IndexName}'.",
                SanitizeLogValue(profile.IndexFullName));
                throw new InvalidOperationException($"Failed to create Elasticsearch index '{profile.IndexFullName}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Elasticsearch index '{IndexName}'.", SanitizeLogValue(profile.IndexFullName));

            throw;
        }
    }

    public async Task DeleteAsync(IIndexProfileInfo profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var indexFullName = !string.IsNullOrWhiteSpace(profile.IndexFullName)
            ? profile.IndexFullName
            : ComposeIndexFullName(profile);

        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName);

        try
        {
            var response = await _elasticClient.Indices.DeleteAsync(indexFullName, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Failed to delete Elasticsearch index '{IndexName}'.",
                SanitizeLogValue(indexFullName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Elasticsearch index '{IndexName}'.", SanitizeLogValue(indexFullName));

            throw;
        }
    }
}
