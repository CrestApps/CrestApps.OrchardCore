using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Infrastructure;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

/// <summary>
/// Display driver for DataSource knowledge base index profiles.
/// Handles embedding deployment selection and default field configuration.
/// </summary>
public sealed class DataSourceIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public DataSourceIndexProfileDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<DataSourceIndexProfileDisplayDriver> stringLocalizer)
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

        return Initialize<EditDataSourceIndexProfileViewModel>("DataSourceIndexProfile_Edit", async model =>
        {
            var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);
            var selectedDeployment = await EmbeddingDeploymentResolver.FindEmbeddingDeploymentAsync(_deploymentManager, metadata);
            var deployments = await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding);

            model.EmbeddingDeploymentId = selectedDeployment?.ItemId ?? metadata.EmbeddingDeploymentId;
            model.EmbeddingDeploymentText = selectedDeployment != null ? GetDeploymentDisplayText(selectedDeployment) : model.EmbeddingDeploymentId;
            model.EmbeddingDeployments = BuildEmbeddingDeploymentItems(deployments, model.EmbeddingDeploymentId);
            model.IsLocked = !string.IsNullOrEmpty(model.EmbeddingDeploymentId) &&
                !string.IsNullOrEmpty(indexProfile.IndexFullName);
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(IndexProfile indexProfile, UpdateEditorContext context)
    {
        if (!CanHandle(indexProfile))
        {
            return null;
        }

        var model = new EditDataSourceIndexProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        // Don't allow changes if already locked.
        if (!string.IsNullOrEmpty(metadata.EmbeddingDeploymentId) &&
            !string.IsNullOrEmpty(indexProfile.IndexFullName))
        {
            return Edit(indexProfile, context);
        }

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

        metadata.EmbeddingDeploymentId = deployment.ItemId;
        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata);

        return Edit(indexProfile, context);
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(DataSourceConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SelectListItem> BuildEmbeddingDeploymentItems(IEnumerable<AIDeployment> deployments, string selectedDeploymentId)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(deployment => deployment.ConnectionNameAlias ?? deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment =>
            {
                SelectListGroup group = null;
                var groupKey = deployment.ConnectionNameAlias ?? deployment.ConnectionName;

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
            })
            .ToList();
    }

    private static string GetDeploymentDisplayText(AIDeployment deployment)
        => string.Equals(deployment.Name, deployment.ModelName, StringComparison.OrdinalIgnoreCase)
            ? deployment.Name
            : $"{deployment.Name} ({deployment.ModelName})";
}
