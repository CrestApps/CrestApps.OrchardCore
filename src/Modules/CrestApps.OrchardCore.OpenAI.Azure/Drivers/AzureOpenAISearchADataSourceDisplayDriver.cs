using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAISearchADataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly AzureAISearchIndexSettingsService _indexSettingsService;

    public AzureOpenAISearchADataSourceDisplayDriver(
        AzureAISearchIndexSettingsService indexSettingsService)
    {
        _indexSettingsService = indexSettingsService;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AISearchImplementationName)
        {
            return null;
        }

        return Initialize<AzureProfileSearchAIViewModel>("AzureOpenAIProfileSearchAI_Edit", async model =>
        {
            var metadata = dataSource.As<AzureAIProfileAISearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.IndexName = metadata.IndexName;

            model.IndexNames = (await _indexSettingsService.GetSettingsAsync())
            .Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.AISearchImplementationName)
        {
            return null;
        }

        var model = new AzureProfileSearchAIViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        dataSource.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = model.IndexName,
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
        });

        return Edit(dataSource, context);
    }
}

