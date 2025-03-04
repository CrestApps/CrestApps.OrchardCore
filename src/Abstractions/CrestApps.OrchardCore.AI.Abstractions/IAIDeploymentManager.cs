using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentManager : INamedModelManager<AIDeployment>
{
    /// <summary>
    /// Asynchronously retrieves a list of model deployments for the specified provider and connection name.
    /// </summary>
    /// <param name="providerName">The name of the provider. Must not be null or empty.</param>
    /// <param name="connectionName">The name of the connection. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing the model deployments for the specified provider and connection.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string providerName, string connectionName);
}
