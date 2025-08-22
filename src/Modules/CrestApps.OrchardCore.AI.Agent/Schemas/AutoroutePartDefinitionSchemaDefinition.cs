using Json.Schema;

namespace CrestApps.OrchardCore.AI.Agent.Schemas;

internal sealed class AutoroutePartDefinitionSchemaDefinition : IContentDefinitionSchemaDefinition
{
    public ContentDefinitionSchemaDefinition Type => ContentDefinitionSchemaDefinition.Part;

    public string Name => "AutoroutePart";

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
                ("AutoroutePartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AllowCustomPath", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Default("{{ ContentItem.DisplayText | slugify }}")
                            .Description("The pattern used to build the Path. Must be valid Liquid syntax.")
                        ),
                        ("ShowHomepageOption", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("AllowUpdatePath", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("AllowDisabled", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("AllowRouteContainedItems", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("ManageContainedItemRoutes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                        ),
                        ("AllowAbsolutePath", new JsonSchemaBuilder()
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
