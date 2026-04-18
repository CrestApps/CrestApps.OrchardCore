using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

public interface IRecipeStep
{
    /// <summary>
    /// Gets the recipe step name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    ValueTask<JsonSchema> GetSchemaAsync();
}
