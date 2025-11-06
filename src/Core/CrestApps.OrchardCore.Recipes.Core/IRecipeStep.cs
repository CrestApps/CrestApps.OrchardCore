using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

public interface IRecipeStep
{
    string Name { get; }

    ValueTask<JsonSchema> GetSchemaAsync();
}
