using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileDeploymentDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIProfileDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileDeploymentViewModel>("AIProfileDeployment_Edit", async model =>
        {
            model.ChatDeploymentId = profile.ChatDeploymentId;
            model.UtilityDeploymentId = profile.UtilityDeploymentId;

            model.ChatDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));
        }).Location("Content:1%Deployments;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {

        var model = new EditProfileDeploymentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.ChatDeploymentId = model.ChatDeploymentId;
        profile.UtilityDeploymentId = model.UtilityDeploymentId;

        return Edit(profile, context);
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
