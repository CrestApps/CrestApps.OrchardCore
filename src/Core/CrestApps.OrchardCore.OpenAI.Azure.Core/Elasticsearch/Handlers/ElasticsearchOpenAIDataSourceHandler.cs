using System.Text;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;

public sealed class ElasticsearchOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private const string _titleFieldName = IndexingConstants.DisplayTextKey + ".keyword";

    private readonly IDocumentManager<ElasticIndexSettingsDocument> _documentManager;
    private readonly ShellSettings _shellSettings;
    private readonly ElasticsearchServerOptions _elasticsearchOptions;
    private readonly ILogger _logger;
    private readonly IAIDataSourceManager _aIDataSourceManager;

    public ElasticsearchOpenAIDataSourceHandler(
        IDocumentManager<ElasticIndexSettingsDocument> documentManager,
        IOptions<ElasticsearchServerOptions> elasticsearchOptions,
        ShellSettings shellSettings,
        ILogger<ElasticsearchOpenAIDataSourceHandler> logger,
        IAIDataSourceManager aIDataSourceManager)
    {
        _documentManager = documentManager;
        _shellSettings = shellSettings;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _logger = logger;
        _aIDataSourceManager = aIDataSourceManager;
    }

    public bool CanHandle(string type)
        => string.Equals(type, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, StringComparison.Ordinal);

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        if (context.Profile is null || !context.Profile.TryGet<AIProfileDataSourceMetadata>(out var metadata))
        {
            return;
        }

        var dataSource = await _aIDataSourceManager.FindByIdAsync(metadata.DataSourceId);

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

        var document = await _documentManager.GetOrCreateImmutableAsync();

        if (!document.ElasticIndexSettings.TryGetValue(dataSourceMetadata.IndexName, out var indexSettings))
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

        DataSourceAuthentication credentials = null;

        if (_elasticsearchOptions.AuthenticationType is not null)
        {
            if (string.Equals("key_and_key_id", _elasticsearchOptions.AuthenticationType, StringComparison.OrdinalIgnoreCase))
            {
                credentials = DataSourceAuthentication.FromKeyAndKeyId(_elasticsearchOptions.Key, _elasticsearchOptions.KeyId);
            }
            else if (string.Equals("encoded_api_key", _elasticsearchOptions.AuthenticationType, StringComparison.OrdinalIgnoreCase))
            {
                credentials = DataSourceAuthentication.FromEncodedApiKey(_elasticsearchOptions.EncodedApiKey);
            }
        }

        options.AddDataSource(new ElasticsearchChatDataSource()
        {
            Endpoint = uri,
            IndexName = GetFullIndexNameInternal(indexSettings.IndexName),
            Authentication = credentials,
            Strictness = dataSourceMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = dataSourceMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = DataSourceQueryType.Simple,
            InScope = true,
            OutputContexts = DataSourceOutputContexts.Citations,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = _titleFieldName,
                FilePathFieldName = IndexingConstants.ContentItemIdKey,
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

    /// <summary>
    /// The following code was copied from https://github.com/OrchardCMS/OrchardCore/blob/5f2ccbdca769f1038e05b5fe432095bc6bd6d5a5/src/OrchardCore/OrchardCore.Search.Elasticsearch.Core/Services/ElasticsearchIndexManager.cs#L662-L682
    /// Since in OrchardCore v3, many services were renamed and this will allow us to keep this library backward compatible.
    /// </summary>
    /// <param name="indexName"></param>
    /// <returns></returns>
    private string GetFullIndexNameInternal(string indexName)
        => GetIndexPrefix() + _separator + indexName;

    private const string _separator = "_";
    private string _indexPrefix;

    private string GetIndexPrefix()
    {
        if (_indexPrefix == null)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(_elasticsearchOptions.IndexPrefix))
            {
                parts.Add(_elasticsearchOptions.IndexPrefix.ToLowerInvariant());
            }

            parts.Add(_shellSettings.Name.ToLowerInvariant());

            _indexPrefix = string.Join(_separator, parts);
        }

        return _indexPrefix;
    }
}
