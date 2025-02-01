using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public static class AIDeploymentStoreExtensions
{
    public static async ValueTask<IEnumerable<AIDeployment>> GetDeploymentsAsync(this IAIDeploymentStore store, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var deployments = await store.GetAllAsync();

        return deployments.Where(deployment => deployment.Source == source);
    }
}
