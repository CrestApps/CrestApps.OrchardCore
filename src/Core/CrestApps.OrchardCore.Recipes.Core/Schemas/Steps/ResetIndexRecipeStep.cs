using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ResetIndex" recipe step — resets search index profiles.
/// </summary>
public sealed class ResetIndexRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "ResetIndex";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ResetIndex").Description("Recipe step discriminator. Must be 'ResetIndex'.")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, reset all index profiles and ignore the IndexNames list.")),
                ("IndexNames", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific index profile names to reset when IncludeAll is false.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
