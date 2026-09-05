using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile template chat mode shape.
/// </summary>
public sealed class AIProfileTemplateChatModeDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateChatModeDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="capabilityService">The deployment capability service.</param>
    /// <param name="speechVoiceMenuService">The speech voice menu service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        IStringLocalizer<AIProfileTemplateChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _speechVoiceMenuService = speechVoiceMenuService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIProfileChatModeViewModel>("AIProfileChatMode_Edit", async model =>
        {
            if (template.Properties.ContainsKey(nameof(ChatModeProfileSettings)))
            {
                var settings = template.GetOrCreate<ChatModeProfileSettings>();
                model.ChatMode = settings.ChatMode;
                model.VoiceName = settings.VoiceName;
            }

            if (template.TryGet<ProfileTemplateMetadata>(out var metadata))
            {
                model.RealtimeDeploymentName = metadata.RealtimeDeploymentName;
            }

            var hasSpeech = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.SpeechToText) != null;
            var realtimeDeployments = await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime);

            model.HasRealtime = realtimeDeployments.Count > 0;
            model.AvailableModes = GetAvailableModes(hasSpeech, model.HasRealtime);
            model.AvailableVoices = hasSpeech ? await GetAvailableVoicesAsync() : [];
            model.RealtimeDeployments = realtimeDeployments
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
                .Select(deployment => new SelectListItem(deployment.Name, deployment.Name))
                .ToList();
        }).Location("Content:8%General;1")
        .RenderWhen(async () =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return false;
            }

            return await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.SpeechToText) != null
                || await HasRealtimeDeploymentAsync();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIProfileChatModeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settings = template.GetOrCreate<ChatModeProfileSettings>();
        settings.ChatMode = model.ChatMode;
        settings.VoiceName = model.ChatMode is ChatMode.Conversation or ChatMode.Realtime
            ? model.VoiceName?.Trim()
            : null;
        template.Put(settings);

        var metadata = template.GetOrCreate<ProfileTemplateMetadata>();
        metadata.RealtimeDeploymentName = model.ChatMode == ChatMode.Realtime
            ? model.RealtimeDeploymentName?.Trim()
            : null;
        template.Put(metadata);

        return Edit(template, context);
    }

    private List<SelectListItem> GetAvailableModes(bool hasSpeech, bool hasRealtime)
    {
        var modes = new List<SelectListItem>
        {
            new(S["Text only"], nameof(ChatMode.TextInput)),
        };

        if (hasSpeech)
        {
            modes.Add(new(S["Audio input"], nameof(ChatMode.AudioInput)));
            modes.Add(new(S["Conversation"], nameof(ChatMode.Conversation)));
        }

        if (hasRealtime)
        {
            modes.Add(new(S["Realtime (speech-to-speech)"], nameof(ChatMode.Realtime)));
        }

        return modes;
    }

    private async Task<bool> HasRealtimeDeploymentAsync()
    {
        var deployments = await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime);

        return deployments.Count > 0;
    }

    private async Task<IEnumerable<SelectListItem>> GetAvailableVoicesAsync()
        => await _speechVoiceMenuService.GetVoiceMenuItemsAsync(deploymentName: null);
}
