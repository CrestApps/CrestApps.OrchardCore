using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Schema for the "AIProfile" recipe step — creates or updates AI profiles.
/// </summary>
public sealed class AIProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "AIProfile";

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
        var profileSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. If provided, used to find existing profile to update.")),
                ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The AI provider source name (e.g., OpenAI, Azure, Ollama). Required for new profiles.")),
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique technical name for the profile.")),
                ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Human-readable display name.")),
                ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Profile description.")),
                ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Initial greeting shown to users.")),
                ("Type", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chat", "Utility", "TemplatePrompt", "Agent").Description("The profile type.")),
                ("TitleType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("InitialPrompt", "Generated").Description("How the session title is generated.")),
                ("PromptTemplate", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Liquid template for TemplatePrompt profiles.")),
                ("PromptSubject", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Prompt subject.")),
                ("OrchestratorName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Orchestrator name.")),
                ("PersonaId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional technical name of the AI persona this profile links to. An empty value unlinks the persona, and leaving the key out keeps the current link.")),
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional name of the provider connection the profile uses.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Chat deployment technical name for the AI model.")),
                ("UtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Utility deployment technical name for the AI model.")),
                ("SystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Shortcut for Properties.AIProfileMetadata.SystemMessage. Ignored when the profile's system message is locked.")),
                ("InitialPrompt", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Shortcut for Properties.AIProfileMetadata.InitialPrompt.")),
                ("Temperature", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Shortcut for Properties.AIProfileMetadata.Temperature.")),
                ("TopP", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Shortcut for Properties.AIProfileMetadata.TopP.")),
                ("FrequencyPenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Shortcut for Properties.AIProfileMetadata.FrequencyPenalty.")),
                ("PresencePenalty", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Shortcut for Properties.AIProfileMetadata.PresencePenalty.")),
                ("MaxTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Shortcut for Properties.AIProfileMetadata.MaxTokens.")),
                ("PastMessagesCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Shortcut for Properties.AIProfileMetadata.PastMessagesCount.")),
                ("UseCaching", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Shortcut for Properties.AIProfileMetadata.UseCaching.")),
                ("LockSystemMessage", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Shortcut for Settings.AIProfileSettings.LockSystemMessage.")),
                ("IsListable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Shortcut for Settings.AIProfileSettings.IsListable.")),
                ("IsRemovable", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Shortcut for Settings.AIProfileSettings.IsRemovable.")),
                ("ChatMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("TextInput", "AudioInput", "Conversation").Description("Shortcut for Settings.ChatModeProfileSettings.ChatMode.")),
                ("VoiceName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Shortcut for Settings.ChatModeProfileSettings.VoiceName.")),
                ("EnableTextToSpeechPlayback", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Shortcut for Settings.ChatModeProfileSettings.EnableTextToSpeechPlayback.")),
                ("CreatedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional creation timestamp in ISO 8601 format.")),
                ("OwnerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional owner identifier.")),
                ("Author", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional author name.")),
                ("Properties", AIProfileRecipeSchemaBuilder.BuildPropertiesSchema("Extended profile properties. Known metadata objects are listed here, and additional feature-specific objects are also allowed.")),
                ("Settings", AIProfileRecipeSchemaBuilder.BuildSettingsSchema("Profile settings. Known settings objects are listed here, and additional feature-specific settings are also allowed.")))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIProfile").Description("Recipe step discriminator. Must be 'AIProfile'.")),
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
