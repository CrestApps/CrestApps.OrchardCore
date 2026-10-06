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
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("dialer profile")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the dialer profile.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the dialer profile.")),
                            ("Mode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Manual", "Preview", "Power", "Progressive", "Predictive").Description("Dialing mode that controls pacing and agent reservation behavior.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the Contact Center voice provider that places calls, or null for the default.")),
                            ("CallsPerAgent", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Number of calls placed per available agent for power dialing.")),
                            ("MaxAttempts", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Maximum number of dialing attempts allowed per activity.")),
                            ("RetryDelayMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Delay, in minutes, before a no-answer activity is retried.")),
                            ("AnsweringMachineDetection", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Disabled", "Standard", "Premium").Description("Whether Power and Progressive dialing asks the provider to screen out answering machines before connecting an agent. A machine-answered call is hung up and retried.")),
                            ("RingTimeoutSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds a Power, Progressive or Predictive call rings before it is given up as unanswered, from 15 to 120 (0 for the default of 30). Automated calls never ring for less than 15 seconds.")),
                            ("PreviewExtensionSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds each extension adds when a preview agent asks for more time to review a record before it is dialed.")),
                            ("MaxPreviewExtensions", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many times one preview offer may be extended.")),
                            ("CallerId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Caller identifier presented to the customer when supported.")),
                            ("AlwaysUseCallerId", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the caller identifier is presented even for an agent who has their own outbound line; when false, agents with a line call from it.")),
                            ("DefaultRegionCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("ISO 3166-1 alpha-2 region a destination is read in when it carries no country calling code.")),
                            ("RespectDoNotCall", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether do-not-call and communication preferences suppress activities.")),
                            ("EnforceCallingWindow", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether calls are restricted by business-hours calendars.")),
                            ("EnforceAbandonmentCap", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether outbound dialing is gated by a rolling abandonment-rate cap.")),
                            ("MaxAbandonmentRatePercent", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Maximum tolerated rolling abandonment rate, expressed as a percentage of calls a live person answered.")),
                            ("AbandonmentSampleFloor", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Minimum number of live-answered calls that must accumulate in the rolling window before the abandonment rate is enforced.")),
                            ("SafeHarborEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an abandoned automated call plays the abandoned-call message instead of being hung up silently. Required when an automated profile enforces the abandonment cap.")),
                            ("SafeHarborMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The abandoned-call message spoken to a person who answered when no agent can be connected. '{company}' is replaced with the site name and '{number}' with the number the call came from.")),
                            ("CallingCalendarId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Default business-hours calendar used to evaluate outbound calls.")),
                            ("RegionalCallingCalendarIds", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A business-hours calendar identifier."))
                                .Description("Region-specific business-hours calendar overrides keyed by ISO 3166-1 alpha-2 region code.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the dialer profile is enabled.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("dialer profile")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The Contact Center dialer profiles to create or update.")))
            .Required("name", "DialerProfiles")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
