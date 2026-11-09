using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Chat.Recipes.Schemas;

/// <summary>
/// Provides the schema definition for AI chat prompt security settings.
/// </summary>
public sealed class PromptSecurityOptionsSchema : SiteSettingsSchemaBase
{
    private static readonly string[] _riskLevels = ["None", "Low", "Medium", "High", "Critical"];

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
                ("EnableChatInteractionInputDelimiters", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether user messages in Chat Interactions are wrapped with boundary delimiters. Chat Interactions get no security preamble or injection blocking.")),
                ("EnableAuditLogging", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether security audit logging is enabled.")),
                ("BlockingThreshold", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(_riskLevels).Default("High").Description("The minimum prompt risk level that triggers blocking, after weighted scoring.")),
                ("LowRiskScoreThreshold", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(10).Description("The aggregate score from which a prompt is suspicious (low risk).")),
                ("MediumRiskScoreThreshold", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(20).Description("The aggregate score from which a prompt is medium risk.")),
                ("HighRiskScoreThreshold", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(35).Description("The aggregate score from which a prompt is high risk.")),
                ("CriticalRiskScoreThreshold", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(50).Description("The aggregate score from which a prompt is critical risk.")),
                ("CustomBlockedPatterns", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Extra regular expressions detected in user prompts. A match scores as critical and is blocked by the default thresholds.")),
                ("MaxMessagesPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of messages allowed within the rate-limit window.")),
                ("RateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The chat message rate-limit window as a .NET TimeSpan string.")),
                ("AnonymousMessageRateLimitTiers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The multi-tier sliding-window message limits applied to anonymous callers. A message is throttled when it would exceed any tier. When empty, anonymous callers fall back to MaxMessagesPerWindow and RateLimitWindow.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of events allowed within the window.")), ("Window", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The sliding-window duration as a .NET TimeSpan string."))).AdditionalProperties(false))),
                ("MaxAnonymousSessionsPerWindow", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of anonymous sessions allowed within the anonymous session-start window. Used only as a fallback when AnonymousSessionStartRateLimitTiers is empty.")),
                ("AnonymousSessionRateLimitWindow", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The anonymous session-start rate-limit window as a .NET TimeSpan string.")),
                ("AnonymousSessionStartRateLimitTiers", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The multi-tier sliding-window limits applied to anonymous session starts. A session start is throttled when it would exceed any tier. When empty, anonymous session starts fall back to MaxAnonymousSessionsPerWindow and AnonymousSessionRateLimitWindow.").Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(("Limit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of events allowed within the window.")), ("Window", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The sliding-window duration as a .NET TimeSpan string."))).AdditionalProperties(false))))
            .AdditionalProperties(false);
}
