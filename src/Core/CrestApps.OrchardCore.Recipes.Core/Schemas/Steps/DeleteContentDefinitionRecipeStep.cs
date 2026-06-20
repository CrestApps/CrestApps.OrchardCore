using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "DeleteContentDefinition" recipe step — deletes content types/parts by name.
/// </summary>
public sealed class DeleteContentDefinitionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "DeleteContentDefinition";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("DeleteContentDefinition").Description("Recipe step discriminator. Must be 'DeleteContentDefinition'.")),
                ("ContentTypes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Content type names to delete.")),
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Reusable content part names to delete.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
