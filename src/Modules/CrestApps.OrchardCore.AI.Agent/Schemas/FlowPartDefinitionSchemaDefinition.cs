using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class FlowPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "FlowPart";

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
                ("FlowPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContainedContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                        ),
                        ("CollapseContainedItems", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        )
                    )
                    .AdditionalProperties(false) // only allow these keys inside FlowPartSettings
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
