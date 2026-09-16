using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterEntryPoint" recipe step — imports inbound entry points that map dialed numbers to a target queue and gate calls by business hours.
/// </summary>
public sealed class ContactCenterEntryPointRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterEntryPoint";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterEntryPoint").Description("Recipe step discriminator. Must be 'ContactCenterEntryPoint'.")),
                ("EntryPoints", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing entry point is updated instead of a new one being created.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the entry point.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the entry point.")),
                            ("DialedNumbers", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A dialed number (DID) served by this entry point."))
                                .Description("The dialed numbers (DIDs) served by this entry point.")),
                            ("TargetQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue calls route to while the entry point is open.")),
                            ("Priority", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Lowest", "Low", "Normal", "High", "Highest").Description("Priority assigned to calls entering through this entry point.")),
                            ("BusinessHoursCalendarId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the business-hours calendar that gates when the entry point is open. When empty, the entry point is always open.")),
                            ("ClosedAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("HoldInQueue", "Voicemail", "Overflow", "Reject").Description("Action taken for calls while the entry point is closed.")),
                            ("OverflowQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue used when 'ClosedAction' is 'Overflow'.")),
                            ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Greeting or announcement shown to the caller.")),
                            ("ClosedMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Message played when the entry point is closed.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the entry point is enabled.")))
                        .AdditionalProperties(true))
                    .Description("The Contact Center entry points to create or update.")))
            .Required("name", "EntryPoints")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
