using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using CrestApps.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.DataSources.AzureAI.Services;

/// <summary>
/// Azure AI Search implementation of <see cref="ISearchIndexManager"/>
/// for creating, deleting, and checking search indexes.
/// </summary>
internal sealed class AzureAISearchIndexManager : ISearchIndexManager
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly ILogger<AzureAISearchIndexManager> _logger;

    public AzureAISearchIndexManager(
        SearchIndexClient searchIndexClient,
        ILogger<AzureAISearchIndexManager> logger)
    {
        _searchIndexClient = searchIndexClient;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(string indexFullName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexFullName);

        try
        {
            await _searchIndexClient.GetIndexAsync(indexFullName, cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of Azure AI Search index '{IndexName}'.", indexFullName);

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
            var azureFields = new List<Azure.Search.Documents.Indexes.Models.SearchField>();

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
            _logger.LogError(ex, "Error creating Azure AI Search index '{IndexName}'.", profile.IndexFullName);

            throw;
        }
    }

    public async Task DeleteAsync(string indexFullName, CancellationToken cancellationToken = default)
    {
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
            _logger.LogError(ex, "Error deleting Azure AI Search index '{IndexName}'.", indexFullName);

            throw;
        }
    }
}
