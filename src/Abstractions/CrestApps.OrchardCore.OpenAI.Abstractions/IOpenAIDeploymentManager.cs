using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI;

public interface IOpenAIDeploymentManager
{
    /// <summary>
    /// Asynchronously deletes the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to be deleted. Must not be null.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is <c>true</c> if the deletion was successful,
    /// and <c>false</c> if the deletion failed (e.g., if the deployment does not exist).
    /// </returns>
    Task<bool> DeleteAsync(OpenAIDeployment profile);

    /// <summary>
    /// Asynchronously retrieves a model deployment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model deployment. Must not be null or empty.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is the <see cref="OpenAIDeployment"/> corresponding
    /// to the specified ID, or <c>null</c> if no deployment with the specified ID is found.
    /// </returns>
    Task<OpenAIDeployment> FindByIdAsync(string id);

    /// <summary>
    /// Asynchronously creates a new model deployment with the given source and optional additional data.
    /// </summary>
    /// <param name="source">The source from which the model deployment is created. Must not be null or empty.</param>
    /// <param name="data">Optional additional data associated with the deployment. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is the newly created <see cref="OpenAIDeployment"/>.
    /// </returns>
    Task<OpenAIDeployment> NewAsync(string source, JsonNode data = null);

    /// <summary>
    /// Asynchronously retrieves a paginated list of model deployments based on the specified pagination and filtering parameters.
    /// </summary>
    /// <param name="page">The page number to retrieve (1-based index).</param>
    /// <param name="pageSize">The number of results to retrieve per page.</param>
    /// <param name="context">The context for the query operation, which may include filtering, sorting, and other criteria. Can be null.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is a <see cref="OpenAIDeploymentResult"/> containing
    /// the paginated list of model deployments and any relevant metadata (such as total count, etc.).
    /// </returns>
    Task<OpenAIDeploymentResult> PageQueriesAsync(int page, int pageSize, QueryContext context);

    /// <summary>
    /// Asynchronously retrieves a list of all model deployments.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is an <see cref="IEnumerable{ModelDeployment}"/>
    /// containing all model deployments.
    /// </returns>
    Task<IEnumerable<OpenAIDeployment>> GetAllAsync();

    /// <summary>
    /// Asynchronously saves or updates the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to be saved or updated. Must not be null.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. This method does not return any value.
    /// </returns>
    Task SaveAsync(OpenAIDeployment profile);

    /// <summary>
    /// Asynchronously updates the specified model deployment with optional additional data.
    /// </summary>
    /// <param name="profile">The model deployment to update. Must not be null.</param>
    /// <param name="data">Optional additional data to update the deployment with. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. This method does not return any value.
    /// </returns>
    Task UpdateAsync(OpenAIDeployment profile, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model deployment.
    /// </summary>
    /// <param name="profile">The model deployment to validate. Must not be null.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The result is a <see cref="OpenAIDeploymentValidateResult"/>
    /// containing the validation results (e.g., success or failure and any associated errors).
    /// </returns>
    Task<OpenAIDeploymentValidateResult> ValidateAsync(OpenAIDeployment profile);
}
