using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ShortcodeTemplates" recipe step — creates or updates shortcode templates.
/// </summary>
public sealed class ShortcodeTemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ShortcodeTemplates";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ShortcodeTemplates").Description("Recipe step discriminator. Must be 'ShortcodeTemplates'.")),
                ("ShortcodeTemplates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Shortcode template content.")),
                            ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Short help text shown to editors.")),
                            ("Usage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Example usage snippet shown to editors.")),
                            ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Default shortcode output or value.")),
                            ("Categories", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                                .Description("Editor categories assigned to the shortcode template.")))
                        .AdditionalProperties(true))
                    .Description("A dictionary keyed by shortcode name.")))
            .Required("name", "ShortcodeTemplates")
            .AdditionalProperties(true)
            .Build();
}
