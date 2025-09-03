using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIDataSourceStore : IReadCatalog<AIDataSource>
{
    /// <summary>
    /// Asynchronously deletes the specified model from the store.
    /// </summary>
    /// <param name="model">The model to delete. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is <c>true</c> if the deletion was successful, <c>false</c> if the model does not exist or could not be deleted.
    /// </returns>
    ValueTask<bool> DeleteAsync(AIDataSource model);

    /// <summary>
    /// Asynchronously creates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to create. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask CreateAsync(AIDataSource model);

    /// <summary>
    /// Asynchronously updates the specified model in the store.
    /// </summary>
    /// <param name="model">The model to update. Must not be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask UpdateAsync(AIDataSource model);

    /// <summary>
    /// Asynchronously saves all pending changes in the store.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask SaveChangesAsync();

    /// <summary>
    /// Asynchronously finds data sources by the given provider-name.
    /// </summary>
    /// <param name="providerName"></param>
    /// <returns></returns>
    ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(string providerName);

    /// <summary>
    /// Asynchronously finds data sources by the given provider-name and type.
    /// </summary>
    /// <param name="providerName"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    ValueTask<IReadOnlyCollection<AIDataSource>> GetAsync(string providerName, string type);
}
