using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the editor for a cascaded realtime deployment and persists the three deployments it chains onto
/// <see cref="CascadedRealtimeMetadata"/>. Only shown for deployments created under the cascaded realtime
/// provider, which own no connection of their own.
/// </summary>
internal sealed class AIDeploymentCascadedRealtimeDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly IAIDeploymentStore _deploymentStore;
    private readonly IAIDeploymentCapabilityService _capabilityService;

    internal readonly IStringLocalizer S;

    public AIDeploymentCascadedRealtimeDisplayDriver(
        IAIDeploymentStore deploymentStore,
        IAIDeploymentCapabilityService capabilityService,
        IStringLocalizer<AIDeploymentCascadedRealtimeDisplayDriver> stringLocalizer)
    {
        _deploymentStore = deploymentStore;
        _capabilityService = capabilityService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        if (!IsCascaded(deployment))
        {
            return null;
        }

        return Initialize<EditDeploymentCascadedRealtimeViewModel>("AIDeploymentCascadedRealtime_Edit", async model =>
        {
            deployment.TryGet<CascadedRealtimeMetadata>(out var metadata);

            model.SpeechToTextDeploymentName = metadata?.SpeechToTextDeploymentName;
            model.ChatDeploymentName = metadata?.ChatDeploymentName;
            model.TextToSpeechDeploymentName = metadata?.TextToSpeechDeploymentName;

            var deployments = (await _deploymentStore.GetAllAsync())
                .Where(candidate => candidate.ItemId != deployment.ItemId && !IsCascaded(candidate))
                .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // The transcribing leg has to expose a realtime client, which is exactly what the realtime
            // feature declares. The other two are ordinary chat and text-to-speech deployments.
            model.SpeechToTextDeployments = BuildSelectList(
                deployments.Where(candidate => _capabilityService.GetCapabilities(candidate).SupportsFeature(AIDeploymentFeatureNames.Realtime)),
                S["Select a deployment that transcribes speech"]);

            model.ChatDeployments = BuildSelectList(
                _capabilityService.WhereCanHoldTextConversation(deployments.Where(candidate => candidate.SupportsPurpose(AIDeploymentPurpose.Chat))),
                S["Select a deployment that generates the reply"]);

            model.TextToSpeechDeployments = BuildSelectList(
                deployments.Where(candidate => candidate.SupportsPurpose(AIDeploymentPurpose.TextToSpeech)),
                S["Select a deployment that speaks the reply"]);
        }).Location("Content:8");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        if (!IsCascaded(deployment))
        {
            return null;
        }

        var model = new EditDeploymentCascadedRealtimeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = new CascadedRealtimeMetadata
        {
            SpeechToTextDeploymentName = model.SpeechToTextDeploymentName?.Trim(),
            ChatDeploymentName = model.ChatDeploymentName?.Trim(),
            TextToSpeechDeploymentName = model.TextToSpeechDeploymentName?.Trim(),
        };

        if (string.IsNullOrEmpty(metadata.SpeechToTextDeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SpeechToTextDeploymentName), S["A speech-to-text deployment is required."]);
        }

        if (string.IsNullOrEmpty(metadata.ChatDeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChatDeploymentName), S["A chat deployment is required."]);
        }

        if (string.IsNullOrEmpty(metadata.TextToSpeechDeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TextToSpeechDeploymentName), S["A text-to-speech deployment is required."]);
        }

        deployment.Put(metadata);

        return Edit(deployment, context);
    }

    private static List<SelectListItem> BuildSelectList(IEnumerable<AIDeployment> deployments, LocalizedString placeholder)
    {
        var items = new List<SelectListItem>
        {
            new(placeholder.Value, string.Empty),
        };

        items.AddRange(deployments.Select(deployment => new SelectListItem(deployment.Name, deployment.Name)));

        return items;
    }

    private static bool IsCascaded(AIDeployment deployment)
        => string.Equals(deployment.ClientName, AIConstants.CascadedRealtimeClientName, StringComparison.OrdinalIgnoreCase);
}
