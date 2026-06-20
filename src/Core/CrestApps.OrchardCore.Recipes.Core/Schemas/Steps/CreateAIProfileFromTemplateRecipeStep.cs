using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "CreateAIProfileFromTemplate" recipe step — creates or updates AI profiles from templates whose source is <c>Profile</c>.
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
                ("TemplateId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Required identifier or name of an AI template whose source is Profile. The template is applied first, then any properties in this object override the generated profile.")),
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
                ("Properties", AIProfileRecipeSchemaBuilder.BuildPropertiesSchema("Overrides extended profile properties. Known metadata objects are listed here, and additional feature-specific objects are also allowed.")),
                ("Settings", AIProfileRecipeSchemaBuilder.BuildSettingsSchema("Overrides profile settings. Known settings objects are listed here, and additional feature-specific settings are also allowed.")))
            .Required("TemplateId")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("CreateAIProfileFromTemplate").Description("Recipe step discriminator. Must be 'CreateAIProfileFromTemplate'.")),
                ("Profiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(profileSchema)
                    .MinItems(1)
                    .Description("The AI profiles to create or update from Profile templates. Each entry starts from the selected template and then applies any explicitly provided overrides.")))
            .Required("name", "Profiles")
            .AdditionalProperties(true)
            .Build();
    }
}
