using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private readonly AzureAISearchIndexSettingsService _azureAISearchIndexSettingsService;
    private readonly IAIDataSourceManager _aIDataSourceManager;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;

    public AzureAISearchOpenAIDataSourceHandler(
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        AzureAISearchIndexSettingsService azureAISearchIndexSettingsService,
        IAIDataSourceManager aIDataSourceManager)
    {
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _azureAISearchIndexSettingsService = azureAISearchIndexSettingsService;
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
        var settings = await _azureAISearchIndexSettingsService.GetSettingsAsync();

        var indexSettings = settings.FirstOrDefault(x => x.IndexName == dataSourceMetadata.IndexName);
        if (indexSettings == null
            || string.IsNullOrEmpty(indexSettings.IndexFullName)
            || !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return;
        }

        var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        options.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexSettings.IndexFullName,
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
        if (keyField == null || keyField.AzureFieldKey == IndexingConstants.ContentItemIdKey)
        {
            return AzureAISearchIndexManager.DisplayTextAnalyzedKey;
        }

        return null;
    }
}
