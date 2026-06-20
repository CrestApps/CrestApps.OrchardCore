using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "azureai-index-reset" recipe step — resets Azure AI Search indexes.
/// </summary>
public sealed class AzureAIIndexResetRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "azureai-index-reset";

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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("azureai-index-reset").Description("Recipe step discriminator. Must be 'azureai-index-reset'.")),
                ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("When true, reset all Azure AI Search indexes and ignore the Indices list.")),
                ("Indices", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Specific Azure AI Search index names to reset when IncludeAll is false.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
