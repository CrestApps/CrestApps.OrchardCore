namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "recipes" recipe step — executes other named recipes.
/// </summary>
public sealed class RecipesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "recipes";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("recipes")),
                ("Values", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("executionid", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .Required("executionid", "name")
                        .AdditionalProperties(true))))
            .Required("name", "Values")
            .AdditionalProperties(true)
            .Build();
}
