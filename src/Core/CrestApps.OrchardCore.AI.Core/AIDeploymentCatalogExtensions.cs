using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides extension methods for AI deployment catalog.
/// </summary>
public static class AIDeploymentCatalogExtensions
{
    /// <summary>
    /// Retrieves the connection display name.
    /// </summary>
    /// <param name="deployment">The deployment.</param>
    public static string GetConnectionDisplayName(this AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        if (!string.IsNullOrWhiteSpace(deployment.ConnectionName))
        {
            return deployment.ConnectionName;
        }

        if (!string.IsNullOrWhiteSpace(deployment.ClientName))
        {
            return deployment.ClientName;
        }

        return deployment.Name;
    }
}
