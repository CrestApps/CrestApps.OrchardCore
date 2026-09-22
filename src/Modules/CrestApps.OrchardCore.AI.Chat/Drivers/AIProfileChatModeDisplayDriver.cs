using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for the AI profile chat mode shape.
/// </summary>
public sealed class AIProfileChatModeDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileChatModeDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="capabilityService">The deployment capability service.</param>
    /// <param name="speechVoiceMenuService">The speech voice menu service.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        ISiteService siteService,
        IStringLocalizer<AIProfileChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _speechVoiceMenuService = speechVoiceMenuService;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileChatModeViewModel>("AIProfileChatMode_Edit", async model =>
        {
            if (profile.TryGetSettings<ChatModeProfileSettings>(out var settings))
            {
                model.ChatMode = settings.ChatMode;
                model.ConversationDeploymentName = settings.ConversationDeploymentName;
                model.VoiceName = settings.VoiceName;
                model.EnableTextToSpeechPlayback = settings.EnableTextToSpeechPlayback;
            }

            // A profile stored before the conversation deployment existed names its speech-to-speech model as
            // its chat deployment, and the chat picker no longer offers it. Show it where it now belongs, so
            // saving the profile writes the current shape. The fold cannot happen while deserializing -- only
            // the deployment's own capability distinguishes such a profile, and that needs the catalog.
            var folded = await _deploymentManager.ResolveConversationModeAsync(
                model.ChatMode,
                model.ConversationDeploymentName,
                profile.ChatDeploymentName,
                hasSpeechToText: false,
                hasTextToSpeech: false);

            if (folded.FoldedFromChatDeployment)
            {
                model.ConversationDeploymentName = folded.RequestedDeploymentName;
                model.ChatMode = ChatMode.Conversation;
            }

            var hasSpeech = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.SpeechToText) != null;
            var realtimeDeployments = await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.Realtime);
            var deploymentSettings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();

            model.AvailableModes = await GetAvailableModesAsync(hasSpeech);
            model.AvailableVoices = hasSpeech ? await GetAvailableVoicesAsync() : [];
            model.AvailableConversationDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Realtime, S["Standalone"].Value);
            model.DefaultRealtimeDeploymentName = deploymentSettings.DefaultRealtimeDeploymentName;

            // The editor mirrors the realtime slot's chain -- the profile's own choice, then the site
            // default, then the first realtime-capable deployment -- to know whether an empty selection
            // still resolves to a model that speaks, and so which voices to offer.
            model.RealtimeDeploymentNames = realtimeDeployments
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .Select(deployment => deployment.Name)
                .ToArray();
        }).Location("Content:1.7%Deployments & Interactions;2")
        .RenderWhen(async () =>
        {
            if (profile.Type != AIProfileType.Chat)
            {
                return false;
            }

            return await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.SpeechToText) != null
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

            // Never written with the resolved value. Empty means "use the site default", and storing what
            // that resolved to today would pin the profile to a model the operator has since replaced.
            settings.ConversationDeploymentName = string.IsNullOrWhiteSpace(model.ConversationDeploymentName)
                ? null
                : model.ConversationDeploymentName.Trim();

            // One voice question, whichever path answers it: a realtime deployment's own voices, or the
            // text-to-speech voices when the conversation runs as the speech-to-text plus text-to-speech
            // cascade.
            settings.VoiceName = model.ChatMode == ChatMode.Conversation
                ? model.VoiceName?.Trim()
                : null;
            settings.EnableTextToSpeechPlayback = model.EnableTextToSpeechPlayback;
        });

        return Edit(profile, context);
    }

    private async Task<List<SelectListItem>> GetAvailableModesAsync(bool hasSpeech)
    {
        // There is no realtime mode. Conversation mode is what asks for a spoken conversation, and the
        // conversation deployment names the model that carries it -- a speech-to-speech session when one
        // resolves, the speech-to-text plus text-to-speech cascade when none does. Conversation is offered
        // whenever either path can serve it.
        var modes = new List<SelectListItem>
        {
            new(S["Text only"], nameof(ChatMode.TextInput)),
        };

        if (hasSpeech)
        {
            modes.Add(new(S["Audio input"], nameof(ChatMode.AudioInput)));
        }

        if (hasSpeech || await HasRealtimeDeploymentAsync())
        {
            modes.Add(new(S["Conversation"], nameof(ChatMode.Conversation)));
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
