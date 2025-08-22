using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class SeoMetaPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "SeoMetaPart";

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
                ("SeoMetaPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayKeywords", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayCustomMetaTags", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayOpenGraph", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayTwitter", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayGoogleSchema", new JsonSchemaBuilder().Type(SchemaValueType.Boolean))
                    )
                    .AdditionalProperties(false) // only allow defined keys inside SeoMetaPartSettings
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
