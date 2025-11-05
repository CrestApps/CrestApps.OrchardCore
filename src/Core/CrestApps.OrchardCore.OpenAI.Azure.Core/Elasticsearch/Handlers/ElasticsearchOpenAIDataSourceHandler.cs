using System.Text;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;

public sealed class ElasticsearchOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private const string _titleFieldName = ContentIndexingConstants.DisplayTextKey + ".keyword";

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ElasticsearchConnectionOptions _elasticsearchOptions;
    private readonly ILogger _logger;
    private readonly IAIDataSourceManager _aIDataSourceManager;

    public ElasticsearchOpenAIDataSourceHandler(
        IIndexProfileStore indexProfileStore,
        IOptions<ElasticsearchConnectionOptions> elasticsearchOptions,
        ILogger<ElasticsearchOpenAIDataSourceHandler> logger,
        IAIDataSourceManager aIDataSourceManager)
    {
        _indexProfileStore = indexProfileStore;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _logger = logger;
        _aIDataSourceManager = aIDataSourceManager;
    }

    public bool CanHandle(string type)
        => string.Equals(type, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, StringComparison.Ordinal);

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        if (!_elasticsearchOptions.ConfigurationExists())
        {
            return;
        }

        if (!dataSource.TryGet<AzureAIProfileElasticsearchMetadata>(out var dataSourceMetadata))
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

        options.AddDataSource(new ElasticsearchChatDataSource()
        {
            Endpoint = uri,
            IndexName = indexProfile.IndexFullName,
            Authentication = credentials,
            Strictness = dataSourceMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = dataSourceMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
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
