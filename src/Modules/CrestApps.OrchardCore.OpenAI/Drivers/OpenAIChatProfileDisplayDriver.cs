using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIDeploymentStore _modelDeploymentStore;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileDisplayDriver(
        IAIDeploymentStore modelDeploymentStore,
        IStringLocalizer<OpenAIChatProfileDisplayDriver> stringLocalizer)
    {
        _modelDeploymentStore = modelDeploymentStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        return Initialize<ChatProfileMetadataViewModel>("OpenAIChatProfileMetadata_Edit", async model =>
        {
            var metadata = profile.As<OpenAIChatProfileMetadata>();

            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PastMessagesCount = metadata.PastMessagesCount;
            model.PresencePenalty = metadata.PresencePenalty;
            model.Temperature = metadata.Temperature;
            model.MaxTokens = metadata.MaxTokens;
            model.TopP = metadata.TopP;

            var azureDeployments = await _modelDeploymentStore.GetAllAsync();

            model.Deployments = azureDeployments.Select(x => new SelectListItem(x.Name, x.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var model = new ChatProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.Put(new OpenAIChatProfileMetadata
        {
            FrequencyPenalty = model.FrequencyPenalty,
            PastMessagesCount = model.PastMessagesCount,
            PresencePenalty = model.PresencePenalty,
            Temperature = model.Temperature,
            MaxTokens = model.MaxTokens,
            TopP = model.TopP,
        });

        return Edit(profile, context);
    }
}
