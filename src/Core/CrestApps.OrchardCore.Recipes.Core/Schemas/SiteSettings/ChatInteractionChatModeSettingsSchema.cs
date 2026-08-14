using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for chat interaction chat mode settings.
/// </summary>
public sealed class ChatInteractionChatModeSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ChatInteractionChatModeSettings";

    /// <summary>
    /// Builds the schema for chat interaction chat mode settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the default chat interaction mode.")
            .Properties(
                ("ChatMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("TextInput", "AudioInput", "Conversation").Description("The default chat input mode for interactions.")),
                ("EnableTextToSpeechPlayback", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to enable text-to-speech playback of AI responses.")))
            .AdditionalProperties(false);
}
