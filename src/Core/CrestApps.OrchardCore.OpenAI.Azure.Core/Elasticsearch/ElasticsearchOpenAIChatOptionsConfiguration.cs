using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

public sealed class ElasticsearchOpenAIChatOptionsConfiguration : IOpenAIChatOptionsConfiguration, IAzureOpenAIDataSourceHandler
{
    private const string _titleFieldName = ContentIndexingConstants.DisplayTextKey + ".keyword";

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ElasticsearchConnectionOptions _elasticsearchOptions;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly ILogger _logger;

    public ElasticsearchOpenAIChatOptionsConfiguration(
        IIndexProfileStore indexProfileStore,
        IOptions<ElasticsearchConnectionOptions> elasticsearchOptions,
        IAIDataSourceManager aIDataSourceManager,
        ILogger<ElasticsearchOpenAIChatOptionsConfiguration> logger)
    {
        _indexProfileStore = indexProfileStore;
        _elasticsearchOptions = elasticsearchOptions.Value;
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

        var indexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();

        if (string.IsNullOrWhiteSpace(indexMetadata?.IndexName))
        {
            return;
        }

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("ElasticsearchIndexProfile", out _))
        {
            var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(indexMetadata.IndexName, ElasticsearchConstants.ProviderName);

            if (indexProfile is null)
            {
                return;
            }

            context.AdditionalProperties ??= [];
            context.AdditionalProperties["ElasticsearchIndexProfile"] = indexProfile;
        }
    }

    public void Configure(CompletionServiceConfigureContext context, ChatCompletionOptions chatCompletionOptions)
    {
        if (!CanHandle(context))
        {
            return;
        }

        if (context.AdditionalProperties is null || context.AdditionalProperties.Count == 0)
        {
            return;
        }

        if (!context.AdditionalProperties.TryGetValue("ElasticsearchIndexProfile", out var pr) ||
           pr is not IndexProfile indexProfile)
        {
            return;
        }

        Uri endpoint;

        if (!string.IsNullOrWhiteSpace(_elasticsearchOptions.CloudId))
        {
            (_, endpoint) = ParseCloudId(_elasticsearchOptions.CloudId);
        }
        else
        {
            endpoint = new Uri(_elasticsearchOptions.Url);
        }

        var authentication = new Dictionary<string, object>();

        if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.KeyIdAndKey)
        {
            authentication["type"] = "key_and_key_id";
            authentication["key"] = _elasticsearchOptions.Key;
            authentication["key_id"] = _elasticsearchOptions.KeyId;
        }
        else if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.Base64ApiKey)
        {
            authentication["type"] = "encoded_api_key";
            authentication["encoded_api_key"] = _elasticsearchOptions.Base64ApiKey;
        }
        else
        {
            throw new InvalidOperationException($"The '{_elasticsearchOptions.AuthenticationType}' is not supported as Authentication type for Elasticsearch AI Data Source. Only '{ElasticsearchAuthenticationType.KeyIdAndKey}' and '{ElasticsearchAuthenticationType.Base64ApiKey}' are supported.");
        }

        var elasticsearchDataSource = new
        {
            type = "elasticsearch",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["index_name"] = indexProfile.IndexFullName,
                ["authentication"] = authentication,
                ["semantic_configuration"] = "default",
                ["query_type"] = "simple",
                ["in_scope"] = true,
                ["fields_mapping"] = new Dictionary<string, object>
                {
                    ["title_field"] = _titleFieldName,
                    ["filepath_field"] = ContentIndexingConstants.ContentItemIdKey,
                },
            },
        };

        // Get RAG parameters from AIProfile metadata
        var ragParams = GetRagParameters(context);
        elasticsearchDataSource.parameters["top_n_documents"] = ragParams.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;
        elasticsearchDataSource.parameters["strictness"] = ragParams.Strictness ?? AzureOpenAIConstants.DefaultStrictness;

        if (!string.IsNullOrWhiteSpace(ragParams.Filter))
        {
            elasticsearchDataSource.parameters["filter"] = ragParams.Filter;
        }

        var dataSources = new List<object>()
            {
                elasticsearchDataSource,
            };

#pragma warning disable SCME0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        if (chatCompletionOptions.Patch.TryGetJson("$.data_sources"u8, out var dataSourcesJson) && dataSourcesJson.Length > 0)
        {
            var sources = JsonSerializer.Deserialize<List<object>>(dataSourcesJson.Span);
            if (sources != null)
            {
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

        if (!string.Equals(context.DataSourceType, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, StringComparison.Ordinal) ||
            !_elasticsearchOptions.ConfigurationExists())
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        var indexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();

        if (string.IsNullOrWhiteSpace(indexMetadata?.IndexName))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(indexMetadata.IndexName, ElasticsearchConstants.ProviderName);

        if (indexProfile is null)
        {
            _logger.LogWarning("Index named '{IndexName}' set as Elasticsearch data-source but not found in Elasticsearch document manager.", indexMetadata.IndexName);
            return;
        }

        Uri uri;

        if (!string.IsNullOrWhiteSpace(_elasticsearchOptions.CloudId))
        {
            (_, uri) = ParseCloudId(_elasticsearchOptions.CloudId);
        }
        else
        {
            uri = new Uri(_elasticsearchOptions.Url);
        }
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        DataSourceAuthentication credentials;

        if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.KeyIdAndKey)
        {
            credentials = DataSourceAuthentication.FromKeyAndKeyId(_elasticsearchOptions.Key, _elasticsearchOptions.KeyId);
        }
        else if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.Base64ApiKey)
        {
            credentials = DataSourceAuthentication.FromEncodedApiKey(_elasticsearchOptions.Base64ApiKey);
        }
        else
        {
            throw new InvalidOperationException($"The '{_elasticsearchOptions.AuthenticationType}' is not supported as Authentication type for Elasticsearch AI Data Source. Only '{ElasticsearchAuthenticationType.KeyIdAndKey}' and '{ElasticsearchAuthenticationType.Base64ApiKey}' are supported.");
        }

        // Get RAG parameters from the profile
        var ragMetadata = indexProfile.As<AzureRagChatMetadata>();

        options.AddDataSource(new ElasticsearchChatDataSource()
        {
            Endpoint = uri,
            IndexName = indexProfile.IndexFullName,
            Authentication = credentials,
            Strictness = ragMetadata?.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = ragMetadata?.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = DataSourceQueryType.Simple,
            InScope = true,
            OutputContexts = DataSourceOutputContexts.Citations,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = _titleFieldName,
                FilePathFieldName = ContentIndexingConstants.ContentItemIdKey,
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

        if (!string.Equals(context.CompletionContext.DataSourceType, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, StringComparison.Ordinal))
        {
            return false;
        }

        return _elasticsearchOptions.ConfigurationExists();
    }

    /// <summary>
    /// This logic is copied from https://github.com/elastic/elastic-transport-net/blob/main/src/Elastic.Transport/Components/NodePool/CloudNodePool.cs#L64-L93
    /// To allow us to determine the Uri to connect to.
    /// </summary>
    /// <param name="cloudId"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static (string, Uri) ParseCloudId(string cloudId)
    {
        if (string.IsNullOrWhiteSpace(cloudId))
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} was null or empty but {exceptionSuffix}", nameof(cloudId));
        }

        var tokens = cloudId.Split([':'], 2);
        if (tokens.Length != 2)
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} not in expected format, {exceptionSuffix}", nameof(cloudId));
        }

        var clusterName = tokens[0];
        var encoded = tokens[1];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} base_64_data is empty, {exceptionSuffix}", nameof(cloudId));
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

        var parts = decoded.Split(['$']);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains less then 2 tokens, {exceptionSuffix}", nameof(cloudId));
        }

        var domainName = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no domain name, {exceptionSuffix}", nameof(cloudId));
        }

        var elasticsearchUuid = parts[1].Trim();
        if (string.IsNullOrWhiteSpace(elasticsearchUuid))
        {
            throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no Elasticsearch UUID, {exceptionSuffix}", nameof(cloudId));
        }

        return (clusterName, new Uri($"https://{elasticsearchUuid}.{domainName}"));
    }

    private const string exceptionSuffix = "should be a string in the form of cluster_name:base_64_data";

    /// <summary>
    /// Gets RAG parameters from AIProfile metadata.
    /// </summary>
    private static (int? Strictness, int? TopNDocuments, string Filter) GetRagParameters(CompletionServiceConfigureContext context)
    {
        if (context.AdditionalProperties is not null &&
            context.AdditionalProperties.TryGetValue("RagMetadata", out var ragMeta) &&
            ragMeta is AzureRagChatMetadata ragMetadata)
        {
            return (ragMetadata.Strictness, ragMetadata.TopNDocuments, ragMetadata.Filter);
        }

        return (null, null, null);
    }
}
