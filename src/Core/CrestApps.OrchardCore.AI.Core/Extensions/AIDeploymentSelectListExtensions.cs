using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Builds the deployment drop-downs every editor shows, from the slot the deployment has to fill.
/// </summary>
/// <remarks>
/// Each editor used to fetch and group deployments itself, so the same twenty lines existed in five
/// drivers and an editor that forgot them offered every deployment on the tenant for a role most of
/// them cannot fill -- a model with no vision capability listed as the one that describes figures.
/// Asking for a slot here is what keeps the list honest.
/// </remarks>
public static class AIDeploymentSelectListExtensions
{
    /// <summary>
    /// Gets the deployments that can fill the given slot, as grouped select list items.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="slotName">The slot the deployment has to fill, from <see cref="AIDeploymentSlotNames"/>.</param>
    /// <param name="standaloneGroupName">
    /// The group heading for a deployment that belongs to no connection. When <see langword="null"/>,
    /// such a deployment is listed without a group.
    /// </param>
    /// <returns>The select list items, grouped by connection.</returns>
    public static async Task<IEnumerable<SelectListItem>> GetSelectListBySlotAsync(
        this IAIDeploymentManager deploymentManager,
        string slotName,
        string standaloneGroupName = null)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);

        var deployments = await deploymentManager.GetAllBySlotAsync(slotName);

        return deployments.ToSelectList(standaloneGroupName);
    }

    /// <summary>
    /// Presents the given deployments as select list items grouped by connection.
    /// </summary>
    /// <param name="deployments">The deployments to present.</param>
    /// <param name="standaloneGroupName">
    /// The group heading for a deployment that belongs to no connection. When <see langword="null"/>,
    /// such a deployment is listed without a group.
    /// </param>
    /// <returns>The select list items, grouped by connection.</returns>
    public static IEnumerable<SelectListItem> ToSelectList(
        this IEnumerable<AIDeployment> deployments,
        string standaloneGroupName = null)
    {
        if (deployments is null)
        {
            return [];
        }

        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(deployment => deployment.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment =>
            {
                var groupKey = deployment.ConnectionName ?? standaloneGroupName;
                SelectListGroup group = null;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup
                    {
                        Name = groupKey,
                    };

                    groups[groupKey] = group;
                }

                // The model name is worth showing only when it is not already the name on screen:
                // "gpt-4-1-mini-vision (gpt-4.1-mini)" says which model answers, "gpt-4.1-mini
                // (gpt-4.1-mini)" says it twice.
                var label = string.Equals(deployment.Name, deployment.ModelName, StringComparison.OrdinalIgnoreCase)
                    ? deployment.Name
                    : $"{deployment.Name} ({deployment.ModelName})";

                return new SelectListItem(label, deployment.Name)
                {
                    Group = group,
                };
            });
    }
}
