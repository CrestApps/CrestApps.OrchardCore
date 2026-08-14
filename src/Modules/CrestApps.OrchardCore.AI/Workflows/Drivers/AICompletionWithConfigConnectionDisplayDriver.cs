using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver that contributes the orchestrator and deployment selection to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Settings tab.
/// </summary>
public sealed class AICompletionWithConfigConnectionDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly OrchestratorOptions _orchestratorOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The AI deployment manager for resolving deployments.</param>
    /// <param name="orchestratorOptions">The orchestrator options.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigConnectionDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IStringLocalizer<AICompletionWithConfigConnectionDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _orchestratorOptions = orchestratorOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        return Initialize<AICompletionWithConfigConnectionViewModel>("AICompletionWithConfigConnection_Edit", async model =>
        {
            var interaction = activity.Interaction;

            model.OrchestratorName = interaction.OrchestratorName;
            model.ChatDeploymentName = interaction.ChatDeploymentName;
            model.UtilityDeploymentName = interaction.UtilityDeploymentName;

            var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();

            if (orchestrators.Count > 1)
            {
                model.Orchestrators = orchestrators
                    .Select(x => new SelectListItem(x.Value.Title ?? x.Key, x.Key))
                    .ToArray();
            }

            model.ChatDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.Chat));
            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.Utility));
        }).Location("Content:2#Content;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigConnectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var interaction = activity.Interaction;

        interaction.OrchestratorName = model.OrchestratorName;
        interaction.ChatDeploymentName = model.ChatDeploymentName;
        interaction.UtilityDeploymentName = model.UtilityDeploymentName;

        activity.Interaction = interaction;

        return Edit(activity, context);
    }

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
