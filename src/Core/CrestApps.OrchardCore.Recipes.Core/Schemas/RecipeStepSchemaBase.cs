using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the recipe step schema base.
/// </summary>
public abstract class RecipeStepSchemaBase : IRecipeStep
{
    private JsonSchema _cached;

    /// <summary>
    /// Gets the name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    protected abstract JsonSchema CreateSchema();
}
