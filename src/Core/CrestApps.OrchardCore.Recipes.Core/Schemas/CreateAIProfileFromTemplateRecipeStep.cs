using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "CreateAIProfileFromTemplate" recipe step — creates or updates AI profiles from profile templates.
/// </summary>
public sealed class CreateAIProfileFromTemplateRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "CreateAIProfileFromTemplate";

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var profileSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TemplateId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Required template identifier (or template name) to load profile defaults from.")),
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. If provided, used to find an existing profile to update.")),
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the AI provider source name.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the profile name.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the human-readable profile display name.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the profile description.")),
                ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the initial greeting shown to users.")),
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "TemplatePrompt", "Agent").Description("Overrides the profile type.")),
                ("TitleType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("InitialPrompt", "Generated").Description("Overrides how the session title is generated.")),
                ("PromptTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the Liquid template for TemplatePrompt profiles.")),
                ("PromptSubject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the prompt subject.")),
                ("OrchestratorName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the orchestrator name.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the chat deployment technical name.")),
                ("UtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Overrides the utility deployment technical name.")),
                ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Overrides extended profile properties including AIProfileMetadata.")),
                ("Settings", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Overrides profile settings.")))
            .Required("TemplateId")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("CreateAIProfileFromTemplate")),
                ("Profiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(profileSchema)
                    .MinItems(1)
                    .Description("The AI profiles to create or update from templates.")))
            .Required("name", "Profiles")
            .AdditionalProperties(true)
            .Build();
    }
}
