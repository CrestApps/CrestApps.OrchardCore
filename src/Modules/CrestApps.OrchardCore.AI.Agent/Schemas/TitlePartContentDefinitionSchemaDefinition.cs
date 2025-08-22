using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class TitlePartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "TitlePart";

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
                ("TitlePartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("RenderTitle", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("Options", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Editable", "GeneratedDisabled", "GeneratedHidden", "EditableRequired")
                            .Default("Editable")
                        ),
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("This string must be a valid Liquid syntax")
                        )
                    )
                    .AdditionalProperties(false) // only those 3 keys are allowed inside
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
