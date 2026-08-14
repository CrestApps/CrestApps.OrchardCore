using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Claude settings.
/// </summary>
public sealed class ClaudeSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ClaudeSettings";

    /// <summary>
    /// Builds the schema for Claude settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the Claude AI chat integration.")
            .Properties(
                ("AuthenticationType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("NotConfigured", "ApiKey").Description("The authentication method used by the Claude integration.")),
                ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The base URL for the Claude API.")),
                ("ProtectedApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The encrypted API key for the Claude provider.")),
                ("DefaultModel", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The default Claude model to use for interactions.")))
            .AdditionalProperties(false);
}
