using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;

    public AzureAISearchOpenAIDataSourceHandler(
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        IIndexProfileStore indexProfileStore,
        IAIDataSourceManager aIDataSourceManager)
    {
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _indexProfileStore = indexProfileStore;
        _aIDataSourceManager = aIDataSourceManager;
    }

    public bool CanHandle(string type)
        => string.Equals(type, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, StringComparison.Ordinal);

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

        if (!dataSource.TryGet<AzureAIProfileAISearchMetadata>(out var dataSourceMetadata))
        {
            return;
        }

        // In OC v3, the `GetAsync` method was changed to accepts an Id instead of a name.
        // Until we drop support for OC v2, we need to first call `GetSettingsAsync` and find index by name.
        var indexProfile = await _indexProfileStore.FindByIndexNameAndProviderAsync(dataSourceMetadata.IndexName, AzureOpenAIConstants.ProviderName);

        if (indexProfile is null ||
            !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return;
        }

        var keyField = indexProfile.As<AzureAISearchIndexMetadata>().IndexMappings?.FirstOrDefault(x => x.IsKey);

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        options.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexProfile.IndexFullName,
            Authentication = DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key),
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
