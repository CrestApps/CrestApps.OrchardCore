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
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("queue")),
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
                            ("InboundChannelEndpointId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the inbound channel endpoint (dialed number or DID) whose calls are routed to this queue.")),
                            ("FirstResponseTargetSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds a messaging contact may wait for a first reply on this queue before the thread is treated as breached. Zero disables the first-response target.")),
                            ("SkillRequirements", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("SkillId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the skill the requirement describes.")),
                                        ("MinimumProficiency", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Proficiency an agent must reach to satisfy the requirement.")),
                                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an agent who does not meet the requirement is excluded. When false, the requirement is a preference that ranks candidates instead of eliminating them.")),
                                        ("RelaxAfterSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description("Seconds a contact may wait before the requirement is dropped, or null when it never is.")))
                                    .AdditionalProperties(true))
                                .Description("What the queue needs from the agent who takes its work: the proficiency of each skill, whether it is required or preferred, and when it relaxes. Skills listed only in 'RequiredSkills' are read as hard requirements at the default proficiency.")),
                            ("OverflowTargets", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("QueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue a waiting caller moves to.")),
                                        ("AfterSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds the caller must have waited before this hop applies.")))
                                    .AdditionalProperties(true))
                                .Description("Ordered overflow chain describing where a waiting caller widens to and after how long. It supersedes the single 'OverflowQueueId' hop.")),
                            ("MaxWaitSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Longest time, in seconds, the queue makes anybody wait. Zero means no limit.")),
                            ("MaxWaitAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Voicemail", "Overflow").Description("What happens to a caller who reaches 'MaxWaitSeconds'.")),
                            ("MaxQueueSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many callers may wait at once. Zero means no limit.")),
                            ("QueueFullAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Voicemail", "Overflow").Description("What happens to a caller who arrives at a full queue.")),
                            ("Treatment", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object | SchemaValueType.Null)
                                .Properties(
                                    ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Message played once when the caller enters the queue.")),
                                    ("AnnouncementIntervalSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How often, in seconds, the periodic announcement repeats. Zero means never.")),
                                    ("AnnouncePosition", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the announcement states the caller's position in line.")),
                                    ("AnnounceEstimatedWait", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the announcement states the estimated wait.")),
                                    ("HoldMusicMediaId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Voice media reference of the hold music.")),
                                    ("CallbackDtmfKey", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Single telephone key that accepts a callback, or null when no callback is offered.")),
                                    ("CallbackOfferAfterSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds the caller waits before the callback is offered.")),
                                    ("MinimumEstimateSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Shortest wait, in seconds, the queue quotes.")),
                                    ("MaximumEstimateSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Longest wait, in seconds, the queue quotes. Past it the announcement says nothing about time.")),
                                    ("AverageHandleTimeSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Configured average handle time, in seconds, used to estimate the wait.")))
                                .AdditionalProperties(true)
                                .Description("What callers hear while they wait: the welcome message, periodic announcements, hold music and the callback offer.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("queue")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The Contact Center queues to create or update.")))
            .Required("name", "Queues")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
