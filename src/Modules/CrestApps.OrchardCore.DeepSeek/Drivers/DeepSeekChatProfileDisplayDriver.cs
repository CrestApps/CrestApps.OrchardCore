using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core;
using CrestApps.OrchardCore.DeepSeek.Core.Models;
using CrestApps.OrchardCore.DeepSeek.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.DeepSeek.Drivers;

public sealed class DeepSeekChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public DeepSeekChatProfileDisplayDriver(
        IAIDeploymentStore deploymentStore,
        IServiceProvider serviceProvider,
        IStringLocalizer<DeepSeekChatProfileDisplayDriver> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != DeepSeekConstants.TechnologyName)
        {
            return null;
        }

        return Initialize<ChatProfileMetadataViewModel>("DeepSeekChatProfileMetadata_Edit", async model =>
        {
            var metadata = profile.As<DeepSeekChatProfileMetadata>();

            model.SystemMessage = metadata.SystemMessage;
            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PastMessagesCount = metadata.PastMessagesCount;
            model.PresencePenalty = metadata.PresencePenalty;
            model.Temperature = metadata.Temperature;
            model.MaxTokens = metadata.MaxTokens;
            model.TopP = metadata.TopP;

            model.IsSystemMessageLocked = profile.GetSettings<DeepSeekChatProfileSettings>().LockSystemMessage;

            var azureDeployments = await _deploymentStore.GetAllAsync();

            model.Deployments = azureDeployments.Select(x => new SelectListItem(x.Name, x.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != DeepSeekConstants.TechnologyName)
        {
            return null;
        }

        var model = new ChatProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = profile.As<DeepSeekChatProfileMetadata>();

        metadata.FrequencyPenalty = model.FrequencyPenalty;
        metadata.PastMessagesCount = model.PastMessagesCount;
        metadata.PresencePenalty = model.PresencePenalty;
        metadata.Temperature = model.Temperature;
        metadata.MaxTokens = model.MaxTokens;
        metadata.TopP = model.TopP;

        var settings = profile.GetSettings<DeepSeekChatProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            metadata.SystemMessage = model.SystemMessage;
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
