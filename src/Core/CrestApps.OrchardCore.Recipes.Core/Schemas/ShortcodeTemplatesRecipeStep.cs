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
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by shortcode name.")))
            .Required("name", "ShortcodeTemplates")
            .AdditionalProperties(true)
            .Build();
}
