using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentManager
{
    /// <summary>
    /// Asynchronously deletes the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to be deleted. Must not be null.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is <c>true</c> if the deletion was successful,
    /// and <c>false</c> if the deletion failed (e.g., if the deployment does not exist).
    /// </returns>
    ValueTask<bool> DeleteAsync(AIDeployment profile);

    /// <summary>
    /// Asynchronously retrieves a model deployment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model deployment. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is the <see cref="AIDeployment"/> corresponding
    /// to the specified ID, or <c>null</c> if no deployment with the specified ID is found.
    /// </returns>
    ValueTask<AIDeployment> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously creates a new model deployment with the given source and optional additional data.
    /// </summary>
    /// <param name="providerName">The source from which the model deployment is created. Must not be null or empty.</param>
    /// <param name="data">Optional additional data associated with the deployment. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is the newly created <see cref="AIDeployment"/>.
    /// </returns>
    ValueTask<AIDeployment> NewAsync(string providerName, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of model deployments based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve (1-based index).</param>
    /// <param name="pageSize">The number of results to retrieve per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, and other criteria. Can be null.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is a <see cref="AIDeploymentResult"/> containing
    /// the paginated list of model deployments and any relevant metadata (such as total count, etc.).
    /// </returns>
    ValueTask<AIDeploymentResult> PageQueriesAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously retrieves a list of all model deployments.
    /// </summary>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{ModelDeployment}"/>
    /// containing all model deployments.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync();

    /// <summary>
    /// Asynchronously saves or updates the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to be saved or updated. Must not be null.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask SaveAsync(AIDeployment profile);

    /// <summary>
    /// Asynchronously updates the specified model deployment with optional additional data.
    /// </summary>
    /// <param name="profile">The model deployment to update. Must not be null.</param>
    /// <param name="data">Optional additional data to update the deployment with. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask UpdateAsync(AIDeployment profile, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to validate. Must not be null.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is a <see cref="AIValidateResult"/>
    /// containing the validation results (e.g., success or failure and any associated errors).
    /// </returns>
    ValueTask<AIValidateResult> ValidateAsync(AIDeployment profile);

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
