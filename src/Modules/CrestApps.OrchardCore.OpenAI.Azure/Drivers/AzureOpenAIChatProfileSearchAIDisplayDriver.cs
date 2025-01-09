using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIChatProfileSearchAIDisplayDriver : DisplayDriver<OpenAIChatProfile>
{
    private readonly AzureAISearchIndexSettingsService _indexSettingsService;

    public AzureOpenAIChatProfileSearchAIDisplayDriver(
        AzureAISearchIndexSettingsService indexSettingsService)
    {
        _indexSettingsService = indexSettingsService;
    }

    public override IDisplayResult Edit(OpenAIChatProfile profile, BuildEditorContext context)
    {
        if (profile.Source is null || profile.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return null;
        }

        return Initialize<AzureChatProfileSearchAIViewModel>("AzureOpenAIChatProfileSearchAI_Edit", async model =>
        {
            var metadata = profile.As<AzureAIChatProfileAISearchMetadata>();

            model.Strictness = metadata.Strictness;
            model.TopNDocuments = metadata.TopNDocuments;
            model.IndexName = metadata.IndexName;
            model.IncludeContentItemCitations = metadata.IncludeContentItemCitations;

            model.IndexNames = (await _indexSettingsService.GetSettingsAsync())
            .Select(i => new SelectListItem(i.IndexName, i.IndexName));
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatProfile profile, UpdateEditorContext context)
    {
        if (profile.Source is null || profile.Source != AzureWithAzureAISearchProfileSource.Key)
        {
            return null;
        }

        var model = new AzureChatProfileSearchAIViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Put(new AzureAIChatProfileAISearchMetadata
        {
            IndexName = model.IndexName,
            Strictness = model.Strictness,
            TopNDocuments = model.TopNDocuments,
            IncludeContentItemCitations = model.IncludeContentItemCitations,
        });

        return Edit(profile, context);
    }
}
