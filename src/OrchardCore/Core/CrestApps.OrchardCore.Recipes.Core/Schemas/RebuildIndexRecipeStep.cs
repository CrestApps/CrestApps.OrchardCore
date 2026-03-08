using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "RebuildIndex" recipe step — rebuilds search index profiles.
/// </summary>
public sealed class RebuildIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "RebuildIndex";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("RebuildIndex")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("IndexNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
