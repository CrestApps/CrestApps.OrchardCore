using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the Contact Center call recording and monitoring settings.
/// </summary>
public sealed class ContactCenterRecordingSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ContactCenterRecordingSettings";

    /// <summary>
    /// Builds the schema for the Contact Center recording settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Recording governance policy every Contact Center voice interaction must satisfy.")
            .Properties(
                ("RecordAllCalls", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether every call is recorded automatically while the Call Recording feature is enabled: a Contact Center call when it connects to an agent, an AI voice agent's call when it is answered, and a number dialed on the soft phone keypad when it is answered.")),
                ("ConsentModel", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("AllParties", "SingleParty").Description("Consent model that governs whether a call may be recorded: every party must consent, or the recording organization's consent is sufficient.")),
                ("RequireExplicitConsent", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether consent must be captured on the interaction before recording may start. Giving the recording disclosure captures it.")),
                ("EnableRecordingDisclosure", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether callers are told the call is recorded, using RecordingDisclosureText.")),
                ("RecordingDisclosureText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).MaxLength(500).Description("The notice callers are given that the call is recorded, spoken word for word by the platform or read out by the agent.")),
                ("DiscloseOnInboundCalls", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the disclosure is spoken to an inbound caller on an entry point before its welcome message, menu or queue.")),
                ("DiscloseOnAIVoiceCalls", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an automated voice agent gives the disclosure before anything else it says.")),
                ("PromptAgentsToDisclose", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an agent on a call whose caller has not heard the disclosure is shown it to read out and confirm.")),
                ("RetentionDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(36500).Description("Days a recording is retained before it becomes eligible for erasure. Zero retains recordings indefinitely.")),
                ("LegalHoldByDefault", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether captured recordings begin under legal hold, which exempts them from retention-driven and subject-request erasure until released.")),
                ("AllowAgentSecurePause", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an agent may pause and resume recording on their own live interaction from the agent desktop.")),
                ("MaxSecurePauseSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(86400).Description("Longest time, in seconds, a recording may stay paused before it resumes automatically. Zero applies no automatic resume.")),
                ("RequirePauseReason", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an agent must supply a reason when pausing recording.")))
            .AdditionalProperties(false);
}
