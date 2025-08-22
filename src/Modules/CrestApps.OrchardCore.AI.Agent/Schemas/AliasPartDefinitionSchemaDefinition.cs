using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class AliasPartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "AliasPart";

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
                ("AliasPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Default("{{ Model.ContentItem.DisplayText | slugify }}")
                            .Description("The pattern used to generate the alias. Must be valid Liquid syntax.")
                        ),
                        ("Options", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Editable", "GeneratedDisabled")
                            .Default("Editable")
                            .Description("Defines whether the alias is editable or generated-disabled.")
                        )
                    )
                    .AdditionalProperties(false) // only allow the two defined keys
                )
            )
            .AdditionalProperties(true); // allow other part-level settings alongside

        _schema = builder.Build();

        return new ValueTask<JsonSchema>(_schema);
    }
}
