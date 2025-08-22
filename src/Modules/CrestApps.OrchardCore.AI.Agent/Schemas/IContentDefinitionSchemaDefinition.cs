using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

public interface IContentDefinitionSchemaDefinition
{
    ContentDefinitionSchemaDefinition Type { get; }

    string Name { get; }

    ValueTask<JsonSchema> GetSettingsSchemaAsync();
}
