using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentManager : IModelManager<AIDeployment>
{
    /// <summary>
    /// Asynchronously retrieves a paginated list of model deployments based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve (1-based index).</param>
    /// <param name="pageSize">The number of results to retrieve per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, and other criteria. Can be null.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is a <see cref="PageResult<AIDeployment>"/> containing
    /// the paginated list of model deployments and any relevant metadata (such as total count, etc.).
    /// </returns>
    ValueTask<PageResult<AIDeployment>> PageAsync(int page, int pageSize, QueryContext context);

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
