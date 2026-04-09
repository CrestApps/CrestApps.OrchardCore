using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

public sealed class ChatInteractionIndexProfileDisplayDriver : DisplayDriver<IndexProfile>
{
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public ChatInteractionIndexProfileDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<ChatInteractionIndexProfileDisplayDriver> stringLocalizer)
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

        return Initialize<ChatInteractionIndexProfileViewModel>("ChatInteractionIndexProfile_Edit", async model =>
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

        var model = new ChatInteractionIndexProfileViewModel();

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
    {
        return string.Equals(AIConstants.AIDocumentsIndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SelectListItem> BuildEmbeddingDeploymentItems(IEnumerable<AIDeployment> deployments, string selectedDeploymentId)
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
