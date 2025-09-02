using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class CommonPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "CommonPart";

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
                ("CommonPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayDateEditor", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("DisplayOwnerEditor", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        )
                    )
                    .AdditionalProperties(false) // only these keys are allowed inside
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
