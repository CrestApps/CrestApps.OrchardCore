using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

public interface IAIProviderConnectionManager : IModelManager<AIProviderConnection>
{
    /// <summary>
    /// Asynchronously retrieves a list of all model deployments.
    /// </summary>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIProviderConnection}"/>
    /// containing all model deployments.
    /// </returns>
    ValueTask<IEnumerable<AIProviderConnection>> GetAllAsync();

    /// <summary>
    /// Asynchronously retrieves a list of model deployments for the specified provider.
    /// </summary>
    /// <param name="providerName">The name of the provider. Must not be null or empty.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The result is an <see cref="IEnumerable{AIProviderConnection}"/>
    /// containing the model deployments for the specified provider.
    /// </returns>
    ValueTask<IEnumerable<AIProviderConnection>> GetAsync(string providerName);
}
