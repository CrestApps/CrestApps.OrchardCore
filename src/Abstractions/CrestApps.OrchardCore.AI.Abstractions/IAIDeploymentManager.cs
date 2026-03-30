using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentManager : INamedSourceCatalogManager<AIDeployment>
{
    /// <summary>
    /// Asynchronously retrieves a list of model deployments for the specified client and connection name.
    /// </summary>
    /// <param name="clientName">The name of the client. Must not be null or empty.</param>
    /// <param name="connectionName">The name of the connection. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing the model deployments for the specified client and connection.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync(string clientName, string connectionName);

    /// <summary>
    /// Asynchronously retrieves all deployments supporting the specified type.
    /// </summary>
    /// <param name="type">The deployment type to filter by.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIDeployment}"/>
    /// containing all deployments matching the specified type.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetByTypeAsync(AIDeploymentType type);

    /// <summary>
    /// Resolves the default deployment of a given type for a specific connection.
    /// Returns the deployment marked as IsDefault for that type on the connection,
    /// or the first deployment supporting that type on the connection if none is marked as default.
    /// </summary>
    ValueTask<AIDeployment> GetDefaultAsync(string clientName, string connectionName, AIDeploymentType type);

    /// <summary>
    /// Resolves a deployment using the full fallback chain:
    /// 1. If deploymentId is provided, returns that specific deployment.
    /// 2. Falls back to the global default deployment for the given type (from DefaultAIDeploymentSettings).
    /// 3. Falls back to the first deployment supporting the requested type within the current scope.
    /// Returns <see langword="null"/> if no deployment can be resolved.
    /// </summary>
    ValueTask<AIDeployment> ResolveOrDefaultAsync(AIDeploymentType type, string deploymentName = null, string clientName = null, string connectionName = null);

    /// <summary>
    /// Gets all deployments of a given type, optionally filtered by client.
    /// Results are suitable for dropdown population, grouped by connection.
    /// </summary>
    ValueTask<IEnumerable<AIDeployment>> GetAllByTypeAsync(AIDeploymentType type, string clientName = null);
}
