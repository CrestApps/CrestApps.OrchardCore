namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "ShortcodeTemplates" recipe step — creates or updates shortcode templates.
/// </summary>
public sealed class ShortcodeTemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ShortcodeTemplates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ShortcodeTemplates")),
                ("ShortcodeTemplates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Usage", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Categories", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
                        .AdditionalProperties(true))
                    .Description("A dictionary keyed by shortcode name.")))
            .Required("name", "ShortcodeTemplates")
            .AdditionalProperties(true)
            .Build();
}
