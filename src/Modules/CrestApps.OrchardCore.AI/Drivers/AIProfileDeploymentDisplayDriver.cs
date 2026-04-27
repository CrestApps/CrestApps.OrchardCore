using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileDeploymentDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly ISiteService _siteService;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The manager for retrieving AI deployments.</param>
    /// <param name="siteService">The site service for accessing site settings.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AIProfileDeploymentDisplayDriver(
        IAIDeploymentManager deploymentManager,
        ISiteService siteService,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _siteService = siteService;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileDeploymentViewModel>("AIProfileDeployment_Edit", async model =>
        {
            var settings = await _siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();
            model.ChatDeploymentName = profile.ChatDeploymentName;
            model.UtilityDeploymentName = profile.UtilityDeploymentName;
            model.ShowMissingDefaultChatDeploymentWarning = string.IsNullOrEmpty(settings.DefaultChatDeploymentName);
            model.ShowMissingDefaultUtilityDeploymentWarning = string.IsNullOrEmpty(settings.DefaultUtilityDeploymentName);

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

        profile.ChatDeploymentName = model.ChatDeploymentName;
        profile.UtilityDeploymentName = model.UtilityDeploymentName;

        return Edit(profile, context);
    }

    private IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                var groupKey = d.ConnectionName ?? S["Standalone"].Value;
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
