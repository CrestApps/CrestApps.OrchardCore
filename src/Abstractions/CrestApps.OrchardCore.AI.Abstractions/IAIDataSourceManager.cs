using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIDataSourceManager : IReadModelManager<AIDataSource>
{
    /// <summary>
    /// Asynchronously deletes the specified model.
    /// </summary>
    /// <param name="dataSource">The model to be deleted.</param>
    /// <returns>
    /// A <see cref="ValueTask{bool}"/> that represents the asynchronous operation.
    /// The result is <c>true</c> if the deletion was successful, <c>false</c> otherwise.
    /// </returns>
    ValueTask<bool> DeleteAsync(AIDataSource dataSource);

    /// <summary>
    /// Asynchronously creates a new model with optional additional data.
    /// </summary>
    /// <param name="profileSource">The provider name this model belongs to.</param>
    /// <param name="type">The type of the data source.</param>
    /// <param name="data">Optional additional data associated with the model. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is the newly created model.
    /// </returns>
    ValueTask<AIDataSource> NewAsync(string profileSource, string type, JsonNode data = null);

    /// <summary>
    /// Asynchronously creates the given model.
    /// </summary>
    /// <param name="dataSource">The model to be created.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask CreateAsync(AIDataSource dataSource);

    /// <summary>
    /// Asynchronously updates the specified model with optional additional data.
    /// </summary>
    /// <param name="model">The model to be updated.</param>
    /// <param name="data">Optional additional data to update the model with. Defaults to <c>null</c>.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation. No result is returned.
    /// </returns>
    ValueTask UpdateAsync(AIDataSource model, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model.
    /// </summary>
    /// <param name="model">The model to be validated.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.
    /// The result is a <see cref="ValidationResultDetails"/> indicating whether the model is valid.
    /// </returns>
    ValueTask<ValidationResultDetails> ValidateAsync(AIDataSource model);

    /// <summary>
    /// Asynchronously retrieves all models associated with the specified source.
    /// </summary>
    /// <param name="profileSource">The source of the models. Must not be <c>null</c> or empty.</param>
    /// <returns>
    /// A <see cref="ValueTask{IEnumerable{T}}"/> representing the asynchronous operation.
    /// The result is a collection of models associated with the given source.
    /// </returns>
    ValueTask<IEnumerable<AIDataSource>> GetAsync(string profileSource, string type);
}
