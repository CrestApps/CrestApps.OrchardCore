using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentManager : IModelManager<AIDeployment>
{
    /// <summary>
    /// Asynchronously retrieves a list of all model deployments.
    /// </summary>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing all model deployments.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a list of model deployments for the specified provider.
    /// </summary>
    /// <param name="providerName">The name of the provider. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing the model deployments for the specified provider.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAsync(string providerName);

    /// <summary>
    /// Asynchronously retrieves a list of model deployments for the specified provider and connection name.
    /// </summary>
    /// <param name="providerName">The name of the provider. Must not be null or empty.</param>
    /// <param name="connectionName">The name of the connection. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing the model deployments for the specified provider and connection.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAsync(string providerName, string connectionName);

    /// <summary>
    /// Asynchronously retrieves a deployment by provider-name and deployment name.
    /// </summary>
    /// <param name="providerName">The name of the provider. Must not be null or empty.</param>
    /// <param name="deploymentName">The name of the deployment. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="AIDeployment"/>
    /// of the model deployment for the given provider-name and deployment-name if one exists.
    /// </returns>
    Task<AIDeployment> FindAsync(string providerName, string deploymentName);
}
