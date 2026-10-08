using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for AI chat prompt security settings.
/// </summary>
public sealed class PromptSecurityOptionsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => nameof(CrestApps.Core.AI.Security.PromptSecurityOptions);

    /// <summary>
    /// Builds the schema for AI chat prompt security settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for AI chat prompt security and abuse throttling.")
            .Properties(
                ("MaxPromptLength", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum allowed prompt length in characters.")),
                ("EnableInjectionDetection", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether prompt injection detection is enabled.")),
                ("EnableOutputFiltering", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether output filtering is enabled.")),
                ("EnableSecurityPreamble", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the hardened security preamble is enabled.")),
                ("EnableInputDelimiters", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether user input delimiters are enabled.")),
                ("EnableAuditLogging", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether security audit logging is enabled.")),
                ("BlockingThreshold", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The minimum prompt risk level that triggers blocking.")),
                ("MaxMessagesPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of messages allowed within the rate-limit window.")),
                ("RateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The chat message rate-limit window as a .NET TimeSpan string.")),
                ("AnonymousMessageRateLimitTiers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The multi-tier sliding-window message limits applied to anonymous callers. A message is throttled when it would exceed any tier. When empty, anonymous callers fall back to MaxMessagesPerWindow and RateLimitWindow.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of events allowed within the window.")), ("Window", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The sliding-window duration as a .NET TimeSpan string."))).AdditionalProperties(false))),
                ("MaxAnonymousSessionsPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of anonymous sessions allowed within the anonymous session-start window. Used only as a fallback when AnonymousSessionStartRateLimitTiers is empty.")),
                ("AnonymousSessionRateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The anonymous session-start rate-limit window as a .NET TimeSpan string.")),
                ("AnonymousSessionStartRateLimitTiers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The multi-tier sliding-window limits applied to anonymous session starts. A session start is throttled when it would exceed any tier. When empty, anonymous session starts fall back to MaxAnonymousSessionsPerWindow and AnonymousSessionRateLimitWindow.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of events allowed within the window.")), ("Window", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The sliding-window duration as a .NET TimeSpan string."))).AdditionalProperties(false))))
            .AdditionalProperties(false);
}
