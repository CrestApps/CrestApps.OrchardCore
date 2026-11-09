using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for chat interaction memory settings.
/// </summary>
public sealed class ChatInteractionMemorySettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "MemoryMetadata";

    /// <summary>
    /// Builds the schema for chat interaction memory settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for user memory in chat interactions.")
            .Properties(
                ("EnableUserMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether to enable per-user memory in chat interactions.")),
                ("EnableSessionMemory", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether the assistant remembers the earlier parts of a long conversation: messages that fall out of the past-messages window are summarized and relevant older turns recalled.")),
                ("EnablePastConversations", new JsonSchemaBuilder().Type(SchemaValueType.Boolean | SchemaValueType.Null).Description("Whether the assistant can look up the signed-in user's past conversations with the same profile.")))
            .AdditionalProperties(false);
}
