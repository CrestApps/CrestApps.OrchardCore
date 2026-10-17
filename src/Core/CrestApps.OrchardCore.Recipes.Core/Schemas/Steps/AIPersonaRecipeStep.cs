using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "AIPersonas" recipe step — creates or updates AI personas.
/// </summary>
public sealed class AIPersonaRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIPersonas";

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
        var personaSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing persona is updated.")),
                ("Name", RecipeStepSchemaBuilders.String().Description("Unique technical name. AI profiles, profile templates and chat interactions link to the persona by this name, so it cannot change once created. A persona with the same name is updated.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("How the persona is shown in editors.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Helps editors choose the right persona.")),
                ("Instructions", RecipeStepSchemaBuilders.String().Description("Liquid persona instructions placed first in the system message of every profile or interaction that links to the persona. Required.")),
                ("AssistantName", RecipeStepSchemaBuilders.String().Description("The name the assistant uses for itself. The model is told its name, and chat screens show it on assistant messages.")),
                ("Language", RecipeStepSchemaBuilders.String().Description("BCP-47 language tag, such as en-US, used as the speech language when nothing else sets one.")),
                ("VoiceName", RecipeStepSchemaBuilders.String().Description("Provider-specific voice the persona speaks with.")),
                ("VoiceDeploymentName", RecipeStepSchemaBuilders.String().Description("Deployment the voice was chosen from. The voice is only used on deployments of the same provider.")),
                ("AvatarUrl", RecipeStepSchemaBuilders.String().Description("Optional avatar image URL shown next to assistant messages.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the persona.")),
                ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Additional properties stored with the persona.")))
            .Required("Name")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "AIPersonas",
            [("personas", RecipeStepSchemaBuilders.Array(personaSchema, 1).Description("The AI personas to create or update."))],
            ["personas"]);
    }
}
