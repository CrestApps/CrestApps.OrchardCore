using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

public interface IRecipeStep
{
    string Name { get; }

    ValueTask<JsonSchema> GetSchemaAsync();
}
