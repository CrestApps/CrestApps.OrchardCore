using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AdminTemplates" recipe step — creates or updates admin Liquid templates.
/// </summary>
public sealed class AdminTemplatesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "AdminTemplates";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AdminTemplates")),
                ("AdminTemplates", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(true))
                    .Description("A dictionary keyed by template name. Each value has a Content property with Liquid markup.")))
            .Required("name", "AdminTemplates")
            .AdditionalProperties(true)
            .Build();
}
