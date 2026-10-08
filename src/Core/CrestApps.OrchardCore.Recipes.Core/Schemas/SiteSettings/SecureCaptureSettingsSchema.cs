using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Contact Center secure capture settings.
/// </summary>
public sealed class SecureCaptureSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SecureCaptureSettings";

    /// <summary>
    /// Builds the schema for the secure capture settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Policy for agent-assisted secure data capture, where the customer enters sensitive data through a one-time link instead of speaking it.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether agents may start a secure capture. When false the platform refuses a capture regardless of permission or provider capability.")),
                ("LinkTimeToLiveSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(30).Maximum(3600).Description("Lifetime, in seconds, of the one-time customer capture link.")),
                ("PauseRecordingDuringCapture", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether starting a capture pauses call recording for its duration.")))
            .AdditionalProperties(false);
}
