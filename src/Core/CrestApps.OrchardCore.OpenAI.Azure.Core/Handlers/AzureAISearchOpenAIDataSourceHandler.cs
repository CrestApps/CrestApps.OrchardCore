using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureAISearchOpenAIDataSourceHandler(
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IIndexProfileStore indexProfileStore,
        IAIDataSourceManager aIDataSourceManager,
        ILogger<AzureAISearchOpenAIDataSourceHandler> logger)
    {
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _indexProfileStore = indexProfileStore;
        _aIDataSourceManager = aIDataSourceManager;
        _logger = logger;
    }

    public bool CanHandle(string type)
        => string.Equals(type, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, StringComparison.Ordinal);

    public async ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        var dataSource = await _aIDataSourceManager.FindByIdAsync(context.DataSourceId);

        if (dataSource is null)
        {
            return;
        }

        if (!dataSource.TryGet<AzureAIProfileAISearchMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(dataSourceMetadata.IndexName, AzureAISearchConstants.ProviderName);

        if (indexProfile is null ||
            !_azureAISearchDefaultOptions.ConfigurationExists())
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

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var authentication = _azureAISearchDefaultOptions.AuthenticationType switch
        {
            AzureAIAuthenticationType.ApiKey => DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key),
            AzureAIAuthenticationType.ManagedIdentity => DataSourceAuthentication.FromSystemManagedIdentity(),
            _ => throw new NotSupportedException($"Unsupported authentication type: {_azureAISearchDefaultOptions.AuthenticationType}"),
        };

        var keyField = indexProfile.As<AzureAISearchIndexMetadata>().IndexMappings?.FirstOrDefault(x => x.IsKey);

        options.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = endpoint,
            IndexName = indexProfile.IndexFullName,
            Authentication = authentication,
            Strictness = dataSourceMetadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = dataSourceMetadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = DataSourceQueryType.Simple,
            InScope = true,
            SemanticConfiguration = "default",
            OutputContexts = DataSourceOutputContexts.Citations,
            Filter = null,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = GetBestTitleField(keyField),
                FilePathFieldName = keyField?.AzureFieldKey,
                ContentFieldSeparator = Environment.NewLine,
            },
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
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
