using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterQueue" recipe step — imports Contact Center work queues that hold and prioritize activities waiting for agents.
/// </summary>
public sealed class ContactCenterQueueRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "ContactCenterQueue";

    /// <summary>
    /// Builds the JSON schema for this recipe step.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("ContactCenterQueue").Description("Recipe step discriminator. Must be 'ContactCenterQueue'.")),
                ("Queues", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional unique identifier. When supplied and found, the existing queue is updated instead of a new one being created.")),
                            ("QueueGroupId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Optional queue-group identifier used for catalog organization and reporting. Queue groups do not affect routing.")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the queue.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the queue.")),
                            ("DefaultPriority", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Lowest", "Low", "Normal", "High", "Highest").Description("Default priority applied to items added to the queue.")),
                            ("RoutingStrategy", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("LongestIdle", "RoundRobin", "LeastBusy").Description("Strategy used to choose which available agent receives the next queued item.")),
                            ("PreferStickyAgent", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether routing prefers the activity's last assigned user when that agent is eligible and available.")),
                            ("EnableSlaAging", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a waiting item's effective priority increases the longer it waits beyond the SLA threshold.")),
                            ("SlaThresholdSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Service-level threshold, in seconds, after which a waiting item breaches SLA.")),
                            ("ReservationTimeoutSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds a reservation remains valid before it expires and the item is re-queued.")),
                            ("UnansweredOfferAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Requeue", "Voicemail", "Reject").Description("What happens when an offered reservation expires before the agent accepts it.")),
                            ("BusinessHoursCalendarId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the business-hours calendar that gates when the queue routes work. When empty, the queue routes around the clock.")),
                            ("AfterHoursAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("HoldInQueue", "Overflow").Description("Action taken for waiting items while the queue's business-hours calendar reports closed.")),
                            ("OverflowQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue that receives overflowed items. When empty, overflow is disabled.")),
                            ("OverflowAfterSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds an item may wait before it overflows to the overflow queue. Zero disables wait-time overflow.")),
                            ("RequiredSkills", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A skill identifier required to handle work from this queue."))
                                .Description("Skills required to be eligible to handle work from this queue.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the queue is enabled for routing.")),
                            ("InboundChannelEndpointId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the inbound channel endpoint (dialed number or DID) whose calls are routed to this queue.")))
                        .AdditionalProperties(true))
                    .Description("The Contact Center queues to create or update.")))
            .Required("name", "Queues")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
