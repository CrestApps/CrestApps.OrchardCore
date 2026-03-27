using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

public abstract class RecipeStepSchemaBase : IRecipeStep
{
    private JsonSchema _cached;

    public abstract string Name { get; }

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    protected abstract JsonSchema CreateSchema();
}
