using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
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

    public ElasticsearchOpenAIChatOptionsConfiguration(
        IIndexProfileStore indexProfileStore,
        IOptions<ElasticsearchConnectionOptions> elasticsearchOptions,
        IAIDataSourceManager aIDataSourceManager)
    {
        _indexProfileStore = indexProfileStore;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _aIDataSourceManager = aIDataSourceManager;
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

        if (dataSource is null || !dataSource.TryGet<AzureAIProfileElasticsearchMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        if (context.AdditionalProperties is null || !context.AdditionalProperties.TryGetValue("ElasticsearchIndexProfile", out _))
        {
            var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(dataSourceMetadata.IndexName, ElasticsearchConstants.ProviderName);

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

        if (context.AdditionalProperties.TryGetValue("DataSource", out var ds) &&
            ds is AIDataSource dataSource && dataSource.TryGet<AzureAIProfileElasticsearchMetadata>(out var dataSourceMetadata))
        {
            elasticsearchDataSource.parameters["top_n_documents"] = dataSourceMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments;
            elasticsearchDataSource.parameters["strictness"] = dataSourceMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness;
        }
        else
        {
            elasticsearchDataSource.parameters["top_n_documents"] = AzureOpenAIConstants.DefaultTopNDocuments;
            elasticsearchDataSource.parameters["strictness"] = AzureOpenAIConstants.DefaultStrictness;
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
            throw new ArgumentException($"Parameter {nameof(cloudId)} decoded base_64_data contains no elasticsearch UUID, {exceptionSuffix}", nameof(cloudId));
        }

        return (clusterName, new Uri($"https://{elasticsearchUuid}.{domainName}"));
    }

    public ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        throw new NotImplementedException();
    }

    private const string exceptionSuffix = "should be a string in the form of cluster_name:base_64_data";
}
