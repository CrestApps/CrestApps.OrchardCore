using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileDisplayDriver(
        IAIDeploymentStore deploymentStore,
        IServiceProvider serviceProvider,
        IStringLocalizer<OpenAIChatProfileDisplayDriver> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != OpenAIConstants.TechnologyName)
        {
            return null;
        }

        return Initialize<ChatProfileMetadataViewModel>("OpenAIChatProfileMetadata_Edit", async model =>
        {
            var metadata = profile.As<OpenAIChatProfileMetadata>();

            model.SystemMessage = metadata.SystemMessage;
            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PastMessagesCount = metadata.PastMessagesCount;
            model.PresencePenalty = metadata.PresencePenalty;
            model.Temperature = metadata.Temperature;
            model.MaxTokens = metadata.MaxTokens;
            model.TopP = metadata.TopP;

            model.IsSystemMessageLocked = profile.GetSettings<OpenAIChatProfileSettings>().LockSystemMessage;

            var azureDeployments = await _deploymentStore.GetAllAsync();

            model.Deployments = azureDeployments.Select(x => new SelectListItem(x.Name, x.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != OpenAIConstants.TechnologyName)
        {
            return null;
        }

        var model = new ChatProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = profile.As<OpenAIChatProfileMetadata>();

        metadata.FrequencyPenalty = model.FrequencyPenalty;
        metadata.PastMessagesCount = model.PastMessagesCount;
        metadata.PresencePenalty = model.PresencePenalty;
        metadata.Temperature = model.Temperature;
        metadata.MaxTokens = model.MaxTokens;
        metadata.TopP = model.TopP;

        var settings = profile.GetSettings<OpenAIChatProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            metadata.SystemMessage = model.SystemMessage;
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
