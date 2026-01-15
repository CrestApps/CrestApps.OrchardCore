using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchOpenAIChatOptionsConfiguration : IOpenAIChatOptionsConfiguration, IAzureOpenAIDataSourceHandler
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureAISearchOpenAIChatOptionsConfiguration(
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IIndexProfileStore indexProfileStore,
        IAIDataSourceManager aIDataSourceManager,
        ILogger<AzureAISearchOpenAIChatOptionsConfiguration> logger)
    {
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _indexProfileStore = indexProfileStore;
        _aIDataSourceManager = aIDataSourceManager;
        _logger = logger;
    }

    public async Task InitializeConfigurationAsync(CompletionServiceConfigureContext context)
    {
        if (!CanHandle(context))
        {
            return;
        }

        AIDataSource dataSource;

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("DataSource", out var ds))
        {
            dataSource = await _aIDataSourceManager.FindByIdAsync(context.CompletionContext.DataSourceId);

            if (dataSource is null)
            {
                return;
            }

            context.AdditionalProperties ??= [];
            context.AdditionalProperties["DataSource"] = dataSource;
        }
        else
        {
            dataSource = ds as AIDataSource;
        }

        if (dataSource is null)
        {
            return;
        }

        // Get index name from new metadata first, fall back to legacy
        var indexName = GetIndexName(dataSource);

        if (string.IsNullOrWhiteSpace(indexName))
        {
            return;
        }

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("AzureAISearchIndexProfile", out _))
        {
            var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(indexName, AzureAISearchConstants.ProviderName);

            if (indexProfile is null)
            {
                return;
            }

            context.AdditionalProperties ??= [];
            context.AdditionalProperties["AzureAISearchIndexProfile"] = indexProfile;
        }
    }

    public void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions)
    {
        if (!CanHandle(context))
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

        if (context.AdditionalProperties is null || context.AdditionalProperties.Count == 0)
        {
            return;
        }

        if (!context.AdditionalProperties.TryGetValue("AzureAISearchIndexProfile", out var pr) ||
           pr is not IndexProfile indexProfile)
        {
            return;
        }

        var authentication = new Dictionary<string, object>();

        if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ApiKey)
        {
            authentication["type"] = "api_key";
            authentication["key"] = _azureAISearchDefaultOptions.Credential?.Key;
        }
        else if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ManagedIdentity)
        {
            authentication["type"] = "system_assigned_managed_identity";
        }
        else
        {
            throw new NotSupportedException($"Unsupported authentication type: {_azureAISearchDefaultOptions.AuthenticationType}");
        }

        var azureDataSource = new
        {
            type = "azure_search",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["index_name"] = indexProfile.IndexFullName,
                ["authentication"] = authentication,
                ["semantic_configuration"] = "default",
                ["query_type"] = "simple",
                ["in_scope"] = true,
            },
        };

        var keyField = indexProfile.As<AzureAISearchIndexMetadata>().IndexMappings?.FirstOrDefault(x => x.IsKey);

        if (keyField is not null)
        {
            azureDataSource.parameters["fields_mapping"] = new Dictionary<string, object>
            {
                ["title_field"] = GetBestTitleField(keyField),
                ["filepath_field"] = keyField.AzureFieldKey,
            };
        }

        // Get RAG parameters from AIProfile first (new pattern), then fall back to legacy metadata on data source
        var ragParams = GetRagParameters(context);
        azureDataSource.parameters["top_n_documents"] = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;
        azureDataSource.parameters["strictness"] = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness;

        if (!string.IsNullOrWhiteSpace(ragParams.Filter))
        {
            azureDataSource.parameters["filter"] = ragParams.Filter;
        }

#pragma warning disable SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var dataSources = new List<object>()
            {
                azureDataSource,
            };

        if (chatCompletionOptions.Patch.TryGetJson("$.data_sources"u8, out var dataSourcesJson) && dataSourcesJson.Length > 0)
        {
            // Deserialize into a list
            var sources = JsonSerializer.Deserialize<List<object>>(dataSourcesJson.Span);
            if (sources != null)
            {
                // Use the list
                foreach (var source in sources)
                {
                    dataSources.Add(source);
                }
            }
        }

        chatCompletionOptions.Patch.Set("$.data_sources"u8, BinaryData.FromObjectAsJson(dataSources));
#pragma warning restore SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        if (string.IsNullOrEmpty(context.DataSourceId) || string.IsNullOrEmpty(context.DataSourceType))
        {
            return;
        }

        if (!string.Equals(context.DataSourceType, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, StringComparison.Ordinal) ||
            !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        // Get index name from new metadata first, fall back to legacy
        var indexName = GetIndexName(dataSource);

        if (string.IsNullOrWhiteSpace(indexName))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(indexName, AzureAISearchConstants.ProviderName);

        var keyField = indexProfile.As<AzureAISearchIndexMetadata>().IndexMappings?.FirstOrDefault(x => x.IsKey);

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        DataSourceAuthentication credentials;

        if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ApiKey)
        {
            credentials = DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key);
        }
        else if (_azureAISearchDefaultOptions.AuthenticationType == AzureAIAuthenticationType.ManagedIdentity)
        {
            credentials = DataSourceAuthentication.FromSystemManagedIdentity();
        }
        else
        {
            throw new NotSupportedException($"Unsupported authentication type: {_azureAISearchDefaultOptions.AuthenticationType}");
        }

        // Get RAG parameters from context (profile metadata) or fall back to legacy data source metadata
        var ragParams = GetRagParametersFromContext(context, dataSource);

        options.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexProfile.IndexFullName,
            Authentication = credentials,
            Strictness = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = DataSourceQueryType.Simple,
            InScope = true,
            SemanticConfiguration = "default",
            OutputContexts = DataSourceOutputContexts.Citations,
            Filter = string.IsNullOrWhiteSpace(ragParams.Filter) ? null : ragParams.Filter,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = GetBestTitleField(keyField),
                FilePathFieldName = keyField?.AzureFieldKey,
                ContentFieldSeparator = Environment.NewLine,
            },
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    private bool CanHandle(CompletionServiceConfigureContext context)
    {
        if (!string.Equals(context.ProviderName, AzureOpenAIConstants.ProviderName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(context.CompletionContext.DataSourceType, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, StringComparison.Ordinal))
        {
            return false;
        }

        return _azureAISearchDefaultOptions.ConfigurationExists();
    }

    private static string GetBestTitleField(AzureAISearchIndexMap keyField)
    {
        if (keyField == null || keyField.AzureFieldKey == ContentIndexingConstants.ContentItemIdKey)
        {
            return AzureAISearchIndexManager.DisplayTextAnalyzedKey;
        }

        return null;
    }

    /// <summary>
    /// Gets the index name from the data source, trying new metadata first, then falling back to legacy.
    /// </summary>
    private static string GetIndexName(AIDataSource dataSource)
    {
        // Try new metadata first
        var newMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();
        if (!string.IsNullOrWhiteSpace(newMetadata?.IndexName))
        {
            return newMetadata.IndexName;
        }

        // Fall back to legacy metadata
#pragma warning disable CS0618 // Type or member is obsolete
        var legacyMetadata = dataSource.As<AzureAIProfileAISearchMetadata>();
        return legacyMetadata?.IndexName;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// Gets RAG parameters from context (profile) first, then falls back to legacy data source metadata.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments, string Filter) GetRagParameters(CompletionServiceConfigureContext context)
    {
        // Try to get from AIProfile metadata (new pattern)
        if (context.AdditionalProperties is not null &&
            context.AdditionalProperties.TryGetValue("RagMetadata", out var ragMeta) &&
            ragMeta is AzureRagChatMetadata ragMetadata)
        {
            return (ragMetadata.Strictness, ragMetadata.TopNDocuments, ragMetadata.Filter);
        }

        // Fall back to legacy data source metadata
        if (context.AdditionalProperties is not null &&
            context.AdditionalProperties.TryGetValue("DataSource", out var ds) &&
            ds is AIDataSource dataSource)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (dataSource.TryGet<AzureAIProfileAISearchMetadata>(out var legacyMetadata))
            {
                return (legacyMetadata.Strictness, legacyMetadata.TopNDocuments, legacyMetadata.Filter);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        return (null, null, null);
    }

    /// <summary>
    /// Gets RAG parameters from context or falls back to data source.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments, string Filter) GetRagParametersFromContext(AzureOpenAIDataSourceContext context, AIDataSource dataSource)
    {
        // Try to get from context (set by completion handler from AIProfile)
        if (context.RagMetadata is not null)
        {
            return (context.RagMetadata.Strictness, context.RagMetadata.TopNDocuments, context.RagMetadata.Filter);
        }

        // Fall back to legacy data source metadata
#pragma warning disable CS0618 // Type or member is obsolete
        if (dataSource.TryGet<AzureAIProfileAISearchMetadata>(out var legacyMetadata))
        {
            return (legacyMetadata.Strictness, legacyMetadata.TopNDocuments, legacyMetadata.Filter);
        }
#pragma warning restore CS0618 // Type or member is obsolete

        return (null, null, null);
    }
}
