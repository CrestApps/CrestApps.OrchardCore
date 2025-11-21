using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureAISearchAICompletionServiceHandler(
        IIndexProfileStore indexProfileStore,
        IAIDataSourceManager aIDataSourceManager,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        ILogger<AzureAISearchAICompletionServiceHandler> logger)
    {
        _indexProfileStore = indexProfileStore;
        _aIDataSourceManager = aIDataSourceManager;
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _logger = logger;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (context.CompletionContext.DataSourceType != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.CompletionContext.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        if (!dataSource.TryGet<AzureAIProfileAISearchMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(dataSourceMetadata.IndexName, AzureAISearchConstants.ProviderName);

        if (indexProfile is null || !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return;
        }

        if (!Uri.TryCreate(_azureAISearchDefaultOptions.Endpoint, UriKind.Absolute, out var endpoint))
        {
            _logger.LogWarning("The Endpoint provided to Azure AI Options contains invalid value. Unable to use to data source.");

            return;
        }

        if (_azureAISearchDefaultOptions.AuthenticationType != AzureAIAuthenticationType.ApiKey ||
            _azureAISearchDefaultOptions.Credential is null)
        {
            _logger.LogWarning("Unsupported authentication type or missing credential.");

            return;
        }

        var keyField = indexProfile.As<AzureAISearchIndexMetadata>().IndexMappings?.FirstOrDefault(x => x.IsKey);

        var newDataSource = new
        {
            type = "azure_search",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["index_name"] = indexProfile.IndexFullName,
                ["strictness"] = dataSourceMetadata.Strictness ?? 3,
                ["top_n"] = dataSourceMetadata.TopNDocuments ?? 5,
                ["query_type"] = "simple",
                ["in_scope"] = true,
                ["output_contexts"] = "citations",
                ["field_mappings"] = new
                {
                    title_field_name = GetBestTitleField(keyField),
                    file_path_field_name = keyField?.AzureFieldKey,
                    content_field_separator = Environment.NewLine,
                },
            },
        };

        if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ManagedIdentity)
        {
            var identityParams = new Dictionary<string, object>()
            {
                ["type"] = "managed_identity"
            };

            if (!string.IsNullOrEmpty(_azureAISearchDefaultOptions.IdentityClientId))
            {
                identityParams["client_id"] = _azureAISearchDefaultOptions.IdentityClientId;
            }

            newDataSource.parameters["authentication"] = identityParams;
        }
        else if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ApiKey)
        {
            newDataSource.parameters["authentication"] = new Dictionary<string, object>
            {
                ["type"] = "api_key",
                ["key"] = _azureAISearchDefaultOptions.Credential?.Key
            };
        }
        else
        {
            throw new InvalidOperationException($"The '{_azureAISearchDefaultOptions.AuthenticationType}' is not supported as Authentication type for AzureAISearch AI Data Source. Only '{AzureAIAuthenticationType.ApiKey}' and '{AzureAIAuthenticationType.ManagedIdentity}' are supported.");
        }

        context.ChatOptions.AdditionalProperties ??= [];
        List<object> dataSources = [];

        if (context.ChatOptions.AdditionalProperties.TryGetValue("data_sources", out var existing))
        {
            var existingSources = JsonSerializer.Deserialize<List<object>>(existing.ToString() ?? "[]");

            if (existingSources != null)
            {
                dataSources.AddRange(existingSources);
            }
        }

        dataSources.Add(newDataSource);

        context.ChatOptions.AdditionalProperties["data_sources"] = BinaryData.FromObjectAsJson(dataSources);

    }

    private static string GetBestTitleField(AzureAISearchIndexMap keyField)
    {
        if (keyField == null || keyField.AzureFieldKey == ContentIndexingConstants.ContentItemIdKey)
        {
            return AzureAISearchIndexManager.DisplayTextAnalyzedKey;
        }

        return null;
    }
}

