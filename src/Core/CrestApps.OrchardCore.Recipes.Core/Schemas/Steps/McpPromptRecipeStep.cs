using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "McpPrompt" recipe step — creates or updates MCP prompt records.
/// </summary>
public sealed class McpPromptRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "McpPrompt";

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
        var argumentSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", RecipeStepSchemaBuilders.String().Description("Unique argument name used inside the MCP prompt template.")),
                ("Title", RecipeStepSchemaBuilders.String().Description("Optional human-friendly argument title shown to clients.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Optional guidance describing what value the argument expects.")),
                ("Required", RecipeStepSchemaBuilders.Boolean().Description("Whether the client must provide this argument.")))
            .Required("Name")
            .AdditionalProperties(true);

        var promptDefinitionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", RecipeStepSchemaBuilders.String().Description("Unique MCP prompt name. This is the authoritative name used during import.")),
                ("Title", RecipeStepSchemaBuilders.String().Description("Optional human-readable title displayed by MCP clients.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Optional description that explains when the prompt should be used.")),
                ("Arguments", RecipeStepSchemaBuilders.Array(argumentSchema).Description("Declared prompt arguments that clients can supply when invoking the prompt.")))
            .Required("Name")
            .AdditionalProperties(true);

        var promptSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing MCP prompt is updated.")),
                ("Name", RecipeStepSchemaBuilders.String().Description("Optional stored prompt name alias kept for export parity. Recipe imports primarily use Prompt.Name.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the prompt.")),
                ("Prompt", promptDefinitionSchema.Description("The MCP prompt definition to create or update.")),
                ("Properties", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("Additional prompt metadata. No built-in MCP prompt metadata objects are currently exported here, but extra keys are allowed for future extensions.")
                    .AdditionalProperties(true)))
            .Required("Prompt")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "McpPrompt",
            [("Prompts", RecipeStepSchemaBuilders.Array(promptSchema, 1).Description("The MCP prompts to create or update."))],
            ["Prompts"]);
    }
}
