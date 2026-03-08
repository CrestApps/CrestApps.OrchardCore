using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "DeleteContentDefinition" recipe step — deletes content types/parts by name.
/// </summary>
public sealed class DeleteContentDefinitionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "DeleteContentDefinition";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("DeleteContentDefinition")),
                ("ContentTypes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
