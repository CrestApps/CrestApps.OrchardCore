using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Defines the contract for recipe step.
/// </summary>
public interface IRecipeStep
{
    /// <summary>
    /// Gets the recipe step name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default);
}
