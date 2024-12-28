using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AzureAIChatProfileSearchAIDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly AzureAISearchIndexSettingsService _indexSettingsService;

    public AzureAIChatProfileSearchAIDisplayDriver(
        AzureAISearchIndexSettingsService indexSettingsService)
    {
        _indexSettingsService = indexSettingsService;
    }

    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        if (model.Source is null || model.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return null;
        }

        return Initialize<AzureAIChatProfileSearchAIViewModel>("AzureAIChatProfileSearchAI_Edit", async m =>
        {
            var metadata = model.As<AzureAIChatProfileAISearchMetadata>();


            m.Strictness = metadata.Strictness;
            m.TopNDocuments = metadata.TopNDocuments;
            m.IndexName = metadata.IndexName;

            m.IndexNames = (await _indexSettingsService.GetSettingsAsync())
            .Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile model, UpdateEditorContext context)
    {
        if (model.Source is null || model.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return null;
        }

        var viewModel = new AzureAIChatProfileSearchAIViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.Put(new AzureAIChatProfileAISearchMetadata
        {
            IndexName = viewModel.IndexName,
            Strictness = viewModel.Strictness,
            TopNDocuments = viewModel.TopNDocuments,
        });

        return Edit(model, context);
    }
}
