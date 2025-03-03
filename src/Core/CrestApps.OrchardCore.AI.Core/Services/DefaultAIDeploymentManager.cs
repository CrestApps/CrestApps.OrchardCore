using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIDeploymentManager : NamedModelManager<AIDeployment>, IAIDeploymentManager
{
    public DefaultAIDeploymentManager(
        INamedModelStore<AIDeployment> deploymentStore,
        IEnumerable<IModelHandler<AIDeployment>> handlers,
        ILogger<DefaultAIDeploymentManager> logger)
        : base(deploymentStore, handlers, logger)
    {
    }

    public async ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string providerName, string connectionName)
    {
        var deployments = (await Store.GetAllAsync())
            .Where(x => x.ProviderName == providerName &&
            (x.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ConnectionNameAlias ?? string.Empty, connectionName, StringComparison.OrdinalIgnoreCase)));

        foreach (var deployment in deployments)
        {
            await LoadAsync(deployment);
        }

        return deployments;
    }
}
