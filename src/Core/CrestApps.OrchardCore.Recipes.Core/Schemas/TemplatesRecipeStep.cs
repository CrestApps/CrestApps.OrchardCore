namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Templates" recipe step — creates or updates Liquid templates.
/// </summary>
public sealed class TemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "Templates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Templates")),
                ("Templates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(true)
                    .Description("A dictionary keyed by template name. Each value has a Content property with Liquid markup.")))
            .Required("name", "Templates")
            .AdditionalProperties(true)
            .Build();
}
