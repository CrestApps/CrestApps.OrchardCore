using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class HtmlBodyPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "HtmlBodyPart";

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
                ("HtmlBodyPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("SanitizeHtml", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Whether to sanitize the HTML input. When true, Liquid templating is disabled.")
                        )
                    )
                    .AdditionalProperties(false) // only the SanitizeHtml key is allowed inside
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
