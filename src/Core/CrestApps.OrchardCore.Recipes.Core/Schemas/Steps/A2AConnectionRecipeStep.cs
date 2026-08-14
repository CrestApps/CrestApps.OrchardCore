using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "A2AConnection" recipe step — creates or updates A2A client connections.
/// </summary>
public sealed class A2AConnectionRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "A2AConnection";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Connection metadata grouped by metadata object name. Known built-in metadata currently lives under A2AConnectionMetadata, and extra objects are allowed for future extensions.")
            .Properties(
                ("A2AConnectionMetadata", KnownRecipeMetadataSchemaBuilders.BuildA2AConnectionMetadataSchema()))
            .AdditionalProperties(true);

        var connectionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing connection is updated.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Human-readable name shown for the A2A connection in the admin UI.")),
                ("Endpoint", RecipeStepSchemaBuilders.String().Description("Absolute HTTP or HTTPS endpoint of the remote A2A server.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve when importing exported records.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve when importing exported records.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the connection.")),
                ("Properties", propertiesSchema))
            .Required("DisplayText", "Endpoint")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "A2AConnection",
            [("Connections", RecipeStepSchemaBuilders.Array(connectionSchema, 1).Description("The A2A connections to create or update."))],
            ["Connections"]);
    }
}
