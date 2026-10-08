using CrestApps.Core;
using CrestApps.Core.AI;
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
/// Display driver for the AI profile template chat mode shape.
/// </summary>
public sealed class AIProfileTemplateChatModeDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly DefaultSpeechVoicePresenter _speechVoiceMenuService;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateChatModeDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="capabilityService">The deployment capability service.</param>
    /// <param name="speechVoiceMenuService">The speech voice menu service.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateChatModeDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        DefaultSpeechVoicePresenter speechVoiceMenuService,
        ISiteService siteService,
        IStringLocalizer<AIProfileTemplateChatModeDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _speechVoiceMenuService = speechVoiceMenuService;
        _siteService = siteService;
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
                model.ConversationDeploymentName = settings.ConversationDeploymentName;
                model.VoiceName = settings.VoiceName;
            }

            var metadata = template.GetOrCreate<ProfileTemplateMetadata>();

            if (string.IsNullOrWhiteSpace(model.ConversationDeploymentName))
            {
                model.ConversationDeploymentName = metadata.ConversationDeploymentName;
            }

            // A template written before realtime became a model capability named its speech-to-speech model
            // separately. That model is the conversation deployment now, and a template that names one is
            // asking for a spoken conversation.
#pragma warning disable CS0618 // Type or member is obsolete - an existing template still applies.
            if (string.IsNullOrWhiteSpace(model.ConversationDeploymentName) && !string.IsNullOrWhiteSpace(metadata.RealtimeDeploymentName))
            {
                model.ConversationDeploymentName = metadata.RealtimeDeploymentName;
                model.ChatMode = ChatMode.Conversation;
            }
#pragma warning restore CS0618

            // A template stored before the conversation deployment existed names its speech-to-speech model
            // as its chat deployment. Show it where it now belongs, so saving the template writes the current
            // shape rather than rewriting it behind the user's back.
            var folded = await _deploymentManager.ResolveConversationModeAsync(
                model.ChatMode,
                model.ConversationDeploymentName,
                metadata.ChatDeploymentName,
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

            // The editor mirrors the realtime slot's chain -- the template's own choice, then the site
            // default, then the first realtime-capable deployment -- to know whether an empty selection
            // still resolves to a model that speaks, and so which voices to offer.
            model.RealtimeDeploymentNames = realtimeDeployments
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .Select(deployment => deployment.Name)
                .ToArray();
        }).Location("Content:1.7%Deployments & Interactions;2")
        .RenderWhen(async () =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return false;
            }

            return await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.SpeechToText) != null
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

        // Never written with the resolved value. Empty means "use the site default", and storing what that
        // resolved to today would pin every profile the template creates to a model the operator has since
        // replaced.
        var conversationDeploymentName = string.IsNullOrWhiteSpace(model.ConversationDeploymentName)
            ? null
            : model.ConversationDeploymentName.Trim();

        var settings = template.GetOrCreate<ChatModeProfileSettings>();
        settings.ChatMode = model.ChatMode;
        settings.ConversationDeploymentName = conversationDeploymentName;

        // One voice question, whichever path answers it: a realtime deployment's own voices, or the
        // text-to-speech voices when the conversation runs as the speech-to-text plus text-to-speech cascade.
        settings.VoiceName = model.ChatMode == ChatMode.Conversation
            ? model.VoiceName?.Trim()
            : null;
        template.Put(settings);

        // The metadata carries the same answer for the templates that are authored as recipes or markdown
        // rather than through this editor, and the legacy field it supersedes is cleared so the two cannot
        // disagree the next time the template is read.
        var metadata = template.GetOrCreate<ProfileTemplateMetadata>();
        metadata.ConversationDeploymentName = conversationDeploymentName;
#pragma warning disable CS0618 // Type or member is obsolete - superseded by ConversationDeploymentName.
        metadata.RealtimeDeploymentName = null;
#pragma warning restore CS0618
        template.Put(metadata);

        return Edit(template, context);
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
