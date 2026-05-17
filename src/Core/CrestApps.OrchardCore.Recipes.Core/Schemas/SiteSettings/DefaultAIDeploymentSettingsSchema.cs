using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for default AI deployment settings.
/// </summary>
public sealed class DefaultAIDeploymentSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "DefaultAIDeploymentSettings";

    /// <summary>
    /// Builds the schema for default AI deployment settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Default AI deployment assignments for each capability type.")
            .Properties(
                ("DefaultChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default chat deployment.")),
                ("DefaultUtilityDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default utility deployment.")),
                ("DefaultEmbeddingDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default embedding deployment.")),
                ("DefaultImageDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default image generation deployment.")),
                ("DefaultSpeechToTextDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default speech-to-text deployment.")),
                ("DefaultTextToSpeechDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The technical name of the default text-to-speech deployment.")),
                ("DefaultTextToSpeechVoiceId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The voice identifier to use for text-to-speech.")))
            .AdditionalProperties(false);
}
