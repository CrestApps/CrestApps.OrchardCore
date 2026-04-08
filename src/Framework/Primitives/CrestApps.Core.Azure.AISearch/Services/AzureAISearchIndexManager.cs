using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Azure.AISearch.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="ISearchIndexManager"/>
/// for creating, deleting, and checking search indexes.
/// </summary>
internal sealed class AzureAISearchIndexManager : ISearchIndexManager
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly AzureAISearchConnectionOptions _options;
    private readonly ILogger<AzureAISearchIndexManager> _logger;

    public AzureAISearchIndexManager(
        SearchIndexClient searchIndexClient,
        IOptions<AzureAISearchConnectionOptions> options,
        ILogger<AzureAISearchIndexManager> logger)
    {
        _searchIndexClient = searchIndexClient;
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

        if (string.IsNullOrEmpty(indexFullName))
        {
            return false;
        }

        try
        {
            await _searchIndexClient.GetIndexAsync(indexFullName, cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Azure AI Search index '{IndexName}' was not found.", SanitizeLogValue(indexFullName));
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of Azure AI Search index '{IndexName}'.", SanitizeLogValue(indexFullName));

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
            var azureFields = new List<SearchField>();

            foreach (var field in fields)
            {
                if (field.FieldType == SearchFieldType.Vector)
                {
                    var vectorField = new SearchField(field.Name, SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = field.VectorDimensions ?? 1536,
                        VectorSearchProfileName = "default-profile",
                    };

                    azureFields.Add(vectorField);
                }
                else
                {
                    var dataType = field.FieldType switch
                    {
                        SearchFieldType.Text or SearchFieldType.Keyword => SearchFieldDataType.String,
                        SearchFieldType.Integer => SearchFieldDataType.Int32,
                        SearchFieldType.Float => SearchFieldDataType.Double,
                        SearchFieldType.DateTime => SearchFieldDataType.DateTimeOffset,
                        _ => SearchFieldDataType.String,
                    };

                    var azureField = new SearchField(field.Name, dataType)
                    {
                        IsKey = field.IsKey,
                        IsFilterable = field.IsFilterable,
                        IsSearchable = field.IsSearchable && field.FieldType == SearchFieldType.Text,
                    };

                    azureFields.Add(azureField);
                }
            }

            var index = new SearchIndex(profile.IndexFullName)
            {
                Fields = azureFields,
                VectorSearch = new VectorSearch
                {
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("default-algorithm"),
                    },
                    Profiles =
                    {
                        new VectorSearchProfile("default-profile", "default-algorithm"),
                    },
                },
            };

            await _searchIndexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Azure AI Search index '{IndexName}'.", SanitizeLogValue(profile.IndexFullName));

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
            await _searchIndexClient.DeleteIndexAsync(indexFullName, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Index already deleted, nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Azure AI Search index '{IndexName}'.", SanitizeLogValue(indexFullName));

            throw;
        }
    }
}
