using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIElasticsearchDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly AzureAISearchIndexSettingsService _indexSettingsService;

    public AzureOpenAIElasticsearchDataSourceDisplayDriver(
        AzureAISearchIndexSettingsService indexSettingsService)
    {
        _indexSettingsService = indexSettingsService;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AISearchImplementationName || dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        return Initialize<AzureProfileElasticsearchViewModel>("AzureOpenAIProfileElasticsearch_Edit", async model =>
        {
            var metadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.IndexName = metadata.IndexName;

            model.IndexNames = (await _indexSettingsService.GetSettingsAsync())
            .Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AISearchImplementationName || dataSource.Type != AzureOpenAIConstants.DataSourceTypes.Elasticsearch)
        {
            return null;
        }

        var model = new AzureProfileElasticsearchViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        dataSource.Put(new AzureAIProfileElasticsearchMetadata
        {
            IndexName = model.IndexName,
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
        });

        return Edit(dataSource, context);
    }
}
