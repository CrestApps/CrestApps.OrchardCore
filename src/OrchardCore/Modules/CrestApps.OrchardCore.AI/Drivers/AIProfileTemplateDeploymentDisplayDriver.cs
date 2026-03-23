using CrestApps.OrchardCore.AI.Core;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateDeploymentDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIProfileTemplateDeploymentDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<EditProfileDeploymentViewModel>("AIProfileDeployment_Edit", async model =>
        {
            if (template.Source != AITemplateSources.Profile)
            {
                return;
            }

            var metadata = template.As<ProfileTemplateMetadata>();
            model.ChatDeploymentId = metadata.ChatDeploymentId;
            model.UtilityDeploymentId = metadata.UtilityDeploymentId;

            model.ChatDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

            model.UtilityDeployments = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Utility));
        }).Location("Content:1%Deployments;2")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditProfileDeploymentViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = template.As<ProfileTemplateMetadata>();
        metadata.ChatDeploymentId = model.ChatDeploymentId;
        metadata.UtilityDeploymentId = model.UtilityDeploymentId;
        template.Put(metadata);

        return Edit(template, context);
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
