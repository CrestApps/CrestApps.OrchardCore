using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceIndexProfileDisplayDriver"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
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
            var embeddingDeploymentName = metadata.GetEmbeddingDeploymentName();
            var selectedDeployment = string.IsNullOrWhiteSpace(embeddingDeploymentName)
                ? null
                : await _deploymentManager.FindByNameAsync(embeddingDeploymentName);
            var deployments = await _deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding);

            model.EmbeddingDeploymentName = selectedDeployment?.Name ?? embeddingDeploymentName;
            model.EmbeddingDeploymentText = selectedDeployment != null ? GetDeploymentDisplayText(selectedDeployment) : model.EmbeddingDeploymentName;
            model.EmbeddingDeployments = BuildEmbeddingDeploymentItems(deployments, model.EmbeddingDeploymentName);
            model.IsLocked = !string.IsNullOrEmpty(model.EmbeddingDeploymentName) &&
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
        if (!string.IsNullOrEmpty(metadata.GetEmbeddingDeploymentName()) &&
            !string.IsNullOrEmpty(indexProfile.IndexFullName))
        {
            return Edit(indexProfile, context);
        }

        if (string.IsNullOrEmpty(model.EmbeddingDeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Embedding deployment is required."]);
            return Edit(indexProfile, context);
        }

        var deployment = await _deploymentManager.FindByNameAsync(model.EmbeddingDeploymentName);

        if (deployment == null || !deployment.SupportsType(AIDeploymentType.Embedding))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["The selected embedding deployment could not be found."]);
            return Edit(indexProfile, context);
        }

        metadata.SetEmbeddingDeploymentName(deployment.Name);
        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata);

        return Edit(indexProfile, context);
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(DataSourceConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SelectListItem> BuildEmbeddingDeploymentItems(IEnumerable<AIDeployment> deployments, string selectedDeploymentName)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(deployment => deployment.GetConnectionDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment =>
            {
                SelectListGroup group = null;
                var groupKey = deployment.GetConnectionDisplayName();

                if (!string.IsNullOrWhiteSpace(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                return new SelectListItem(GetDeploymentDisplayText(deployment), deployment.Name)
                {
                    Group = group,
                    Selected = string.Equals(deployment.Name, selectedDeploymentName, StringComparison.OrdinalIgnoreCase),
                };
            })
            .ToList();
    }

    private static string GetDeploymentDisplayText(AIDeployment deployment)
        => string.Equals(deployment.Name, deployment.ModelName, StringComparison.OrdinalIgnoreCase)
            ? deployment.Name
            : $"{deployment.Name} ({deployment.ModelName})";
}
