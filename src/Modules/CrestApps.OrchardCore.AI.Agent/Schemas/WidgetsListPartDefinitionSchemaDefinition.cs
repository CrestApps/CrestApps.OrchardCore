using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class WidgetsListPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "WidgetsListPart";

    private JsonSchema _schema;

    public ValueTask<JsonSchema> GetSettingsSchemaAsync()
    {
        if (_schema != null)
        {
            return new ValueTask<JsonSchema>(_schema);
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("WidgetsListPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Zones", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                        )
                    )
                    .AdditionalProperties(false) // only allow defined keys inside WidgetsListPartSettings
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
