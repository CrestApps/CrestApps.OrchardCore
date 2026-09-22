using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Chat.Interactions.Settings;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver for the chat interaction connection shape.
/// </summary>
public sealed class ChatInteractionConnectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISiteService _siteService;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="aiOptions">The ai options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionConnectionDisplayDriver(
        IAIDeploymentManager deploymentManager,
        ISiteService siteService,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<ChatInteractionConnectionDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _siteService = siteService;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // The chat and utility deployment selectors are rendered as separate shapes so the metadata-driven
        // model parameter editors (for example reasoning effort) can be injected immediately after each of
        // their corresponding model selections.
        async ValueTask PopulateAsync(EditChatInteractionConnectionViewModel model)
        {
            var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();
            var site = await _siteService.GetSiteSettingsAsync();

            // The chat deployment is the text model this interaction talks to, so the picker offers the chat
            // slot only -- a speech-to-speech model cannot answer a typed turn. The model that carries a
            // spoken conversation is named separately, below.
            var chatDeployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat)).ToList();

            model.ChatDeploymentName = interaction.ChatDeploymentName;
            model.UtilityDeploymentName = interaction.UtilityDeploymentName;
            model.ConversationDeploymentName = interaction.ConversationDeploymentName;
            model.ConversationDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Realtime);
            model.ConversationModeEnabled = site.GetOrCreate<ChatInteractionChatModeSettings>().ChatMode == ChatMode.Conversation;
            model.ShowMissingDefaultChatDeploymentWarning = string.IsNullOrEmpty(settings.DefaultChatDeploymentName);
            model.ShowMissingDefaultUtilityDeploymentWarning = string.IsNullOrEmpty(settings.DefaultUtilityDeploymentName);
            model.ChatDeployments = chatDeployments.ToSelectList();
            // Vision is the imageInput capability, which is opt-in: a genuinely vision-capable model declares
            // it rather than being inferred from a flag nobody remembered to tick.
            model.DeploymentVisionSupport = chatDeployments
                .Where(SupportsVision)
                .ToDictionary(deployment => deployment.Name, _ => true, StringComparer.OrdinalIgnoreCase);
            model.DefaultChatDeploymentSupportsVision = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat) is { } defaultChatDeployment
                && SupportsVision(defaultChatDeployment);

            model.UtilityDeployments = await _deploymentManager.GetSelectListBySlotAsync(AIDeploymentSlotNames.Utility);
        }

        return Combine(
            Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionChatConnection_Edit", PopulateAsync)
                .Location("Parameters:3#Settings;1"),
            // The model that speaks, separate from the text model above. Naming one is how an interaction
            // opts into a spoken conversation, so it sits directly beneath the deployment it is not.
            Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionConversationDeployment_Edit", PopulateAsync)
                .Location("Parameters:3.4#Settings;1"),
            // The voice belongs to whatever carries the conversation, so it sits with the conversation
            // deployment rather than beside the chat's send controls. Hidden until the client sees that a
            // realtime deployment actually resolves.
            View("ChatInteractionRealtimeVoice_Edit", interaction)
                .Location("Parameters:3.5#Settings;1"),
            Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionUtilityConnection_Edit", PopulateAsync)
                .Location("Parameters:3.7#Settings;1"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new EditChatInteractionConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        interaction.ChatDeploymentName = model.ChatDeploymentName;
        interaction.UtilityDeploymentName = model.UtilityDeploymentName;

        // Never written with the resolved value. Empty means "use the site default", and storing what that
        // resolved to today would pin the interaction to a model the operator has since replaced.
        interaction.ConversationDeploymentName = string.IsNullOrWhiteSpace(model.ConversationDeploymentName)
            ? null
            : model.ConversationDeploymentName.Trim();

        return Edit(interaction, context);
    }

    // imageInput is opt-in, so a deployment that declares no capability metadata does not claim vision.
    private static bool SupportsVision(AIDeployment deployment)
        => deployment.TryGet<AIDeploymentMetadata>(out var metadata) &&
            metadata.SupportsFeature(AIDeploymentFeatureNames.ImageInput);

}
