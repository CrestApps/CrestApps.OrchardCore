using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIDeploymentStore
{
    /// <summary>
    /// Asynchronously deletes a specified deployment from the store.
    /// </summary>
    /// <param name="deployment">The deployment to delete. Must not be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is a boolean indicating
    /// whether the deployment was successfully deleted. Returns <c>true</c> if the deletion succeeded,
    /// and <c>false</c> if the deployment could not be deleted (e.g., if it does not exist).
    /// </returns>
    ValueTask<bool> DeleteAsync(AIDeployment deployment);

    /// <summary>
    /// Asynchronously finds a deployment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier (ID) of the deployment. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is the <see cref="AIDeployment"/> if found,
    /// or <c>null</c> if no deployment with the specified identifier exists in the store.
    /// </returns>
    ValueTask<AIDeployment> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously finds a deployment by its name.
    /// </summary>
    /// <param name="name">The name of the deployment. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is the <see cref="AIDeployment"/> if found,
    /// or <c>null</c> if no deployment with the specified name exists in the store.
    /// </returns>
    ValueTask<AIDeployment> FindByNameAsync(string name);

    /// <summary>
    /// Asynchronously retrieves all deployments in the store.
    /// </summary>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is an <see cref="IEnumerable{ModelDeployment}"/>
    /// containing all deployments available in the store.
    /// </returns>
    ValueTask<IEnumerable<AIDeployment>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a paged list of model deployments based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve, where the index is 1-based.</param>
    /// <param name="pageSize">The number of deployments to retrieve per page.</param>
    /// <param name="context">The query context containing additional filtering, sorting, and search criteria. Can be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. The task result is a <see cref="AIDeploymentResult"/> object,
    /// which contains the list of model deployments for the requested page, along with metadata for pagination (such as total count, etc.).
    /// </returns>
    ValueTask<AIDeploymentResult> PageAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously saves or updates the specified model deployment in the store.
    /// </summary>
    /// <param name="deployment">The model deployment to save or update. Must not be null.</param>
    /// <returns>
    /// A ValueTask representing the asynchronous operation. This method does not return any value.
    /// </returns>
    ValueTask SaveAsync(AIDeployment deployment);
}
