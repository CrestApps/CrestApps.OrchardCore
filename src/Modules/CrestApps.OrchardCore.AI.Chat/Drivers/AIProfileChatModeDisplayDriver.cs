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
/// Display driver for the AI profile chat mode shape.
/// </summary>
public sealed class AIProfileChatModeDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileChatModeDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="capabilityService">The deployment capability service.</param>
    /// <param name="speechVoiceMenuService">The speech voice menu service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        IStringLocalizer<AIProfileChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _speechVoiceMenuService = speechVoiceMenuService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileChatModeViewModel>("AIProfileChatMode_Edit", async model =>
        {
            if (profile.TryGetSettings<ChatModeProfileSettings>(out var settings))
            {
                model.ChatMode = settings.ChatMode;
                model.VoiceName = settings.VoiceName;
                model.EnableTextToSpeechPlayback = settings.EnableTextToSpeechPlayback;
            }

            var hasSpeech = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.SpeechToText) != null;
            var hasRealtime = await HasRealtimeDeploymentAsync();

            model.AvailableModes = GetAvailableModes(hasSpeech, hasRealtime);
            model.AvailableVoices = hasSpeech ? await GetAvailableVoicesAsync() : [];
        }).Location("Content:8%General;1")
        .RenderWhen(async () =>
        {
            if (profile.Type != AIProfileType.Chat)
            {
                return false;
            }

            return await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentPurpose.SpeechToText) != null
                || await HasRealtimeDeploymentAsync();
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Type != AIProfileType.Chat)
        {
            return null;
        }

        var model = new AIProfileChatModeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<ChatModeProfileSettings>(settings =>
        {
            settings.ChatMode = model.ChatMode;
            settings.VoiceName = model.ChatMode == ChatMode.Conversation
                ? model.VoiceName?.Trim()
                : null;
            settings.EnableTextToSpeechPlayback = model.EnableTextToSpeechPlayback;
        });

        return Edit(profile, context);
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
