using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Recipes.Core;

public interface IRecipeStep
{
    string Name { get; }

    ValueTask<JsonSchema> GetSchemaAsync();
}
