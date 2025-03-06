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

public sealed class AzureOpenAIProfileSearchAIDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly AzureAISearchIndexSettingsService _indexSettingsService;

    public AzureOpenAIProfileSearchAIDisplayDriver(
        AzureAISearchIndexSettingsService indexSettingsService)
    {
        _indexSettingsService = indexSettingsService;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        if (profile.Source is null || profile.Source != AzureOpenAIConstants.AISearchImplementationName)
        {
            return null;
        }

        return Initialize<AzureProfileSearchAIViewModel>("AzureOpenAIProfileSearchAI_Edit", async model =>
        {
            var metadata = profile.As<AzureAIProfileAISearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.IndexName = metadata.IndexName;

            model.IndexNames = (await _indexSettingsService.GetSettingsAsync())
            .Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Source is null || profile.Source != AzureOpenAIConstants.AISearchImplementationName)
        {
            return null;
        }

        var model = new AzureProfileSearchAIViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Put(new AzureAIProfileAISearchMetadata
        {
            IndexName = model.IndexName,
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
        });

        return Edit(profile, context);
    }
}
