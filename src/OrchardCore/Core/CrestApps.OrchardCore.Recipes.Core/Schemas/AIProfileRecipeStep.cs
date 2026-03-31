using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "AIProfile" recipe step — creates or updates AI profiles.
/// </summary>
public sealed class AIProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIProfile";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var profileSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. If provided, used to find existing profile to update.")),
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The AI provider source name (e.g., OpenAI, Azure, Ollama). Required for new profiles.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique technical name for the profile.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Human-readable display name.")),
                ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Initial greeting shown to users.")),
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "TemplatePrompt").Description("The profile type.")),
                ("TitleType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("InitialPrompt", "Generated").Description("How the session title is generated.")),
                ("PromptTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Liquid template for TemplatePrompt profiles.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Chat deployment technical name for the AI model.")),
                ("UtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Utility deployment technical name for the AI model.")),
                ("ChatDeploymentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Legacy chat deployment selector. Use ChatDeploymentName instead.")),
                ("UtilityDeploymentId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Legacy utility deployment selector. Use UtilityDeploymentName instead.")),
                ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Extended profile properties including AIProfileMetadata.")),
                ("Settings", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true).Description("Profile settings.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIProfile")),
                ("Profiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(profileSchema)
                    .MinItems(1)
                    .Description("The AI profiles to create or update.")))
            .Required("name", "Profiles")
            .AdditionalProperties(true)
            .Build();
    }
}
