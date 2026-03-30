using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateDeploymentDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        ISiteService siteService,
        IStringLocalizer<AIProfileTemplateDeploymentDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _siteService = siteService;
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
            var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();
            model.ChatDeploymentName = metadata.ChatDeploymentName;
            model.UtilityDeploymentName = metadata.UtilityDeploymentName;
            model.ShowMissingDefaultChatDeploymentWarning = string.IsNullOrEmpty(settings.DefaultChatDeploymentName);
            model.ShowMissingDefaultUtilityDeploymentWarning = string.IsNullOrEmpty(settings.DefaultUtilityDeploymentName);

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
        metadata.ChatDeploymentName = model.ChatDeploymentName;
        metadata.UtilityDeploymentName = model.UtilityDeploymentName;
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
