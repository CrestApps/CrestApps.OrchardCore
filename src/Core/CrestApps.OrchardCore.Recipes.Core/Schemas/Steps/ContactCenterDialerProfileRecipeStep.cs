using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterDialerProfile" recipe step — imports outbound dialing configurations that tie a campaign and queue to a dialing mode and provider.
/// </summary>
public sealed class ContactCenterDialerProfileRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterDialerProfile";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterDialerProfile").Description("Recipe step discriminator. Must be 'ContactCenterDialerProfile'.")),
                ("DialerProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing dialer profile is updated instead of a new one being created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the dialer profile.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the dialer profile.")),
                            ("Mode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Manual", "Preview", "Power", "Progressive", "Predictive").Description("Dialing mode that controls pacing and agent reservation behavior.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the Contact Center voice provider that places calls, or null for the default.")),
                            ("CallsPerAgent", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Number of calls placed per available agent for power dialing.")),
                            ("MaxAttempts", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum number of dialing attempts allowed per activity.")),
                            ("RetryDelayMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Delay, in minutes, before a no-answer activity is retried.")),
                            ("CallerId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Caller identifier presented to the customer when supported.")),
                            ("DefaultRegionCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("ISO 3166-1 alpha-2 region a destination is read in when it carries no country calling code.")),
                            ("RespectDoNotCall", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether do-not-call and communication preferences suppress activities.")),
                            ("EnforceCallingWindow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether calls are restricted by business-hours calendars.")),
                            ("EnforceAbandonmentCap", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether outbound dialing is gated by a rolling abandonment-rate cap.")),
                            ("MaxAbandonmentRatePercent", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Maximum tolerated rolling abandonment rate, expressed as a percentage of calls a live person answered.")),
                            ("AbandonmentSampleFloor", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Minimum number of live-answered calls that must accumulate in the rolling window before the abandonment rate is enforced.")),
                            ("SafeHarborEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an abandoned automated call plays a safe-harbor announcement instead of being dropped silently.")),
                            ("SafeHarborMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Safe-harbor announcement played to a live party when no agent connects in time.")),
                            ("CallingCalendarId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Default business-hours calendar used to evaluate outbound calls.")),
                            ("RegionalCallingCalendarIds", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A business-hours calendar identifier."))
                                .Description("Region-specific business-hours calendar overrides keyed by ISO 3166-1 alpha-2 region code.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the dialer profile is enabled.")))
                        .AdditionalProperties(true))
                    .Description("The Contact Center dialer profiles to create or update.")))
            .Required("name", "DialerProfiles")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
