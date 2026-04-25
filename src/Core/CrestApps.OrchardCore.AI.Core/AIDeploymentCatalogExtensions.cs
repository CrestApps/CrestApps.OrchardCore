using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

public static class AIDeploymentCatalogExtensions
{
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
