using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
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
            // The chat picker asks "what can this interaction talk to", so it lists the text-capable
            // deployments and the realtime (speech-to-speech) ones together.
            var chatDeployments = (await _deploymentManager.GetConversationalDeploymentsAsync()).ToList();

            model.ChatDeploymentName = interaction.ChatDeploymentName;
            model.UtilityDeploymentName = interaction.UtilityDeploymentName;
            model.ShowMissingDefaultChatDeploymentWarning = string.IsNullOrEmpty(settings.DefaultChatDeploymentName);
            model.ShowMissingDefaultUtilityDeploymentWarning = string.IsNullOrEmpty(settings.DefaultUtilityDeploymentName);
            model.ChatDeployments = BuildGroupedDeploymentItems(chatDeployments);
            // Vision is the imageInput capability, which is opt-in: a genuinely vision-capable model declares
            // it rather than being inferred from a flag nobody remembered to tick.
            model.DeploymentVisionSupport = chatDeployments
                .Where(SupportsVision)
                .ToDictionary(deployment => deployment.Name, _ => true, StringComparer.OrdinalIgnoreCase);
            model.DefaultChatDeploymentSupportsVision = await _deploymentManager.ResolveSlotAsync(AIDeploymentSlotNames.Chat) is { } defaultChatDeployment
                && SupportsVision(defaultChatDeployment);

            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Utility));
        }

        return Combine(
            Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionChatConnection_Edit", PopulateAsync)
                .Location("Parameters:3#Settings;1"),
            // The voice belongs to the chat deployment, so it sits with it rather than beside the chat's
            // send controls. Hidden until the client sees that the selected deployment is realtime-capable.
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

        return Edit(interaction, context);
    }

    // imageInput is opt-in, so a deployment that declares no capability metadata does not claim vision.
    private static bool SupportsVision(AIDeployment deployment)
        => deployment.TryGet<AIDeploymentMetadata>(out var metadata) &&
            metadata.SupportsFeature(AIDeploymentFeatureNames.ImageInput);

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                var groupKey = d.ConnectionName;
                SelectListGroup group = null;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };

                    groups[groupKey] = group;
                }

                var label = string.Equals(d.Name, d.ModelName, StringComparison.OrdinalIgnoreCase)
                ? d.Name
                : $"{d.Name} ({d.ModelName})";

                return new SelectListItem(label, d.Name) { Group = group };
            });
    }
}
