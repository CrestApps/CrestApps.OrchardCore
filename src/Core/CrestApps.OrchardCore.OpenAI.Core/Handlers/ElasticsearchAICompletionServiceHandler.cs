using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public sealed class ElasticsearchAICompletionServiceHandler : IAICompletionServiceHandler
{
    private const string _titleFieldName = ContentIndexingConstants.DisplayTextKey + ".keyword";

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ElasticsearchConnectionOptions _elasticsearchOptions;
    private readonly ILogger _logger;
    private readonly IAIDataSourceManager _aIDataSourceManager;

    public ElasticsearchAICompletionServiceHandler(
        IIndexProfileStore indexProfileStore,
        IOptions<ElasticsearchConnectionOptions> elasticsearchOptions,
        ILogger<ElasticsearchAICompletionServiceHandler> logger,
        IAIDataSourceManager aIDataSourceManager)
    {
        _indexProfileStore = indexProfileStore;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _logger = logger;
        _aIDataSourceManager = aIDataSourceManager;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (context.CompletionContext.DataSourceType != ElasticsearchConstants.ProviderName)
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.CompletionContext.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        if (!_elasticsearchOptions.ConfigurationExists())
        {
            return;
        }

        if (!dataSource.TryGet<OpenAIProfileElasticsearchMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(dataSourceMetadata.IndexName, ElasticsearchConstants.ProviderName);

        if (indexProfile is null)
        {
            _logger.LogWarning("Index named '{IndexName}' set as Elasticsearch data-source but not found in Elasticsearch document manager.", dataSourceMetadata.IndexName);
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

        context.ChatOptions.AdditionalProperties ??= [];

        var newDataSource = new
        {
            type = "elasticsearch",
            parameters = new Dictionary<string, object>
            {
                ["endpoint"] = uri,
                ["index_name"] = indexProfile.IndexFullName,
                ["strictness"] = dataSourceMetadata.Strictness ?? 3,
                ["top_n"] = dataSourceMetadata.TopNDocuments ?? 5,
                ["query_type"] = "simple",
                ["semantic_configuration"] = "default",
                ["in_scope"] = true,
                ["output_contexts"] = "citations",
                ["field_mappings"] = new
                {
                    title_field_name = _titleFieldName,
                    file_path_field_name = ContentIndexingConstants.ContentItemIdKey,
                    content_field_separator = Environment.NewLine,
                },
            },
        };

        if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.KeyIdAndKey)
        {
            newDataSource.parameters["authentication"] = new
            {
                type = "KeyAndKeyId",
                key = _elasticsearchOptions.Key,
                key_id = _elasticsearchOptions.KeyId,
            };
        }
        else if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.Base64ApiKey)
        {
            newDataSource.parameters["authentication"] = new
            {
                type = "EncodedApiKey",
                encoded_api_key = _elasticsearchOptions.Base64ApiKey
            };
        }
        else if (_elasticsearchOptions.AuthenticationType == ElasticsearchAuthenticationType.ApiKey)
        {
            newDataSource.parameters["api_key"] = _elasticsearchOptions.ApiKey;
        }
        else
        {
            throw new InvalidOperationException($"The '{_elasticsearchOptions.AuthenticationType}' is not supported as Authentication type for Elasticsearch AI Data Source. Only '{ElasticsearchAuthenticationType.ApiKey}', '{ElasticsearchAuthenticationType.KeyIdAndKey}' and '{ElasticsearchAuthenticationType.Base64ApiKey}' are supported.");
        }

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

    private const string exceptionSuffix = "should be a string in the form of cluster_name:base_64_data";
}
