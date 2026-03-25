using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionConnectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public ChatInteractionConnectionDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<ChatInteractionConnectionDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        var connectionResult = Initialize<EditChatInteractionConnectionViewModel>("ChatInteractionConnection_Edit", async model =>
        {
            model.ChatDeploymentId = interaction.ChatDeploymentId;
            model.UtilityDeploymentId = interaction.UtilityDeploymentId;

            model.ChatDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));

        }).Location("Parameters:3#Settings;1");

        return connectionResult;
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {

        var model = new EditChatInteractionConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        interaction.ChatDeploymentId = model.ChatDeploymentId;
        interaction.UtilityDeploymentId = model.UtilityDeploymentId;

        return Edit(interaction, context);
    }

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionNameAlias ?? d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                var groupKey = d.ConnectionNameAlias ?? d.ConnectionName;

                if (!groups.TryGetValue(groupKey, out var group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                return new SelectListItem(d.Name, d.ItemId) { Group = group };
            });
    }
}
