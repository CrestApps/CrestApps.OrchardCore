using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

/// <summary>
/// Display driver for the AI memory index profile shape.
/// </summary>
public sealed class AIMemoryIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryIndexProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIMemoryIndexProfileDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIMemoryIndexProfileDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(IndexProfile indexProfile, BuildEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        return Initialize<AIMemoryIndexProfileViewModel>("AIMemoryIndexProfile_Edit", async model =>
        {
            var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);
            var selectedDeployment = await EmbeddingDeploymentResolver.FindEmbeddingDeploymentAsync(_deploymentManager, metadata);
            var deployments = await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding);

            model.EmbeddingDeploymentId = selectedDeployment?.ItemId ?? metadata.EmbeddingDeploymentId;
            model.EmbeddingDeployments = BuildEmbeddingDeploymentItems(deployments, model.EmbeddingDeploymentId);
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfile indexProfile, UpdateEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        var model = new AIMemoryIndexProfileViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.EmbeddingDeploymentId))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Embedding deployment is required."]);
            return Edit(indexProfile, context);
        }

        var deployment = await _deploymentManager.FindByIdAsync(model.EmbeddingDeploymentId);

        if (deployment == null || !deployment.SupportsType(AIDeploymentType.Embedding))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["The selected embedding deployment could not be found."]);
            return Edit(indexProfile, context);
        }

        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);
        metadata.EmbeddingDeploymentId = deployment.ItemId;

        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata);

        return Edit(indexProfile, context);
    }

    private static bool CanHandle(IndexProfile indexProfile)
        => string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<SelectListItem> BuildEmbeddingDeploymentItems(IEnumerable<AIDeployment> deployments, string selectedDeploymentId)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(deployment => deployment.Name ?? deployment.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(deployment =>
            {
                SelectListGroup group = null;
                var groupKey = deployment.ConnectionName;

                if (!string.IsNullOrWhiteSpace(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                return new SelectListItem(GetDeploymentDisplayText(deployment), deployment.ItemId)
                {
                    Group = group,
                    Selected = string.Equals(deployment.ItemId, selectedDeploymentId, StringComparison.OrdinalIgnoreCase),
                };
            });
    }

    private static string GetDeploymentDisplayText(AIDeployment deployment)
        => string.Equals(deployment.Name, deployment.ModelName, StringComparison.OrdinalIgnoreCase)
            ? deployment.Name
            : $"{deployment.Name} ({deployment.ModelName})";
}
