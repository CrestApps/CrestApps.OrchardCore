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
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("entry point")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Unique name of the entry point.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Description of the entry point.")),
                            ("DialedNumbers", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A dialed number (DID) served by this entry point."))
                                .Description("The dialed numbers (DIDs) served by this entry point.")),
                            ("TargetType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Queue", "Agent").Description("Whether calls route to a queue ('TargetQueueId') or ring one agent directly ('TargetAgentId').")),
                            ("TargetAgentId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the agent profile calls ring directly when 'TargetType' is 'Agent'. There is no queue fallback.")),
                            ("VoicemailEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether an unanswered direct-to-agent call is sent to the agent's voicemail. When disabled the caller keeps ringing until answered or they hang up. Applies only when 'TargetType' is 'Agent'.")),
                            ("RingTimeoutSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Seconds a direct-to-agent call rings before it is sent to the agent's voicemail. Applies only when 'TargetType' is 'Agent' and 'VoicemailEnabled' is true.")),
                            ("VoicemailGreetingText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Default text-to-speech voicemail greeting for calls through this entry point, used when the agent reached has not recorded their own greeting.")),
                            ("VoicemailRecipientAgentId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the agent profile whose voicemail inbox receives messages left on this line when the call has no agent of its own.")),
                            ("VoicemailDestination", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("AgentInbox", "QueueSharedBox").Description("Where a queue line delivers a message the call has no agent for: the inbox of 'VoicemailRecipientAgentId' or the shared voicemail box of the queue the caller was in. Does not apply to an agent-target entry point.")),
                            ("IvrFlow", IvrFlow()),
                            ("TargetQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue calls route to while the entry point is open.")),
                            ("Priority", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Lowest", "Low", "Normal", "High", "Highest").Description("Priority assigned to calls entering through this entry point.")),
                            ("BusinessHoursCalendarId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the business-hours calendar that gates when the entry point is open. When empty, the entry point is always open.")),
                            ("ClosedAction", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("HoldInQueue", "Voicemail", "Overflow", "Reject").Description("Action taken for calls while the entry point is closed.")),
                            ("OverflowQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the queue used when 'ClosedAction' is 'Overflow'.")),
                            ("WelcomeMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Message spoken once to a caller while the entry point is open, before the IVR menu or the target.")),
                            ("ClosedMessage", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Message spoken to a caller while the entry point is closed, before the closed action is applied.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the entry point is enabled.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("entry point")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The Contact Center entry points to create or update.")))
            .Required("name", "EntryPoints")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder IvrAction(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Properties(
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("RouteToQueue", "RouteToAgent", "Voicemail", "ExternalTransfer", "Repeat", "SubMenu").Description("The kind of action.")),
                ("TargetId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the action acts on: a queue identifier, an agent profile identifier, a child node identifier, or an approved external transfer destination identifier.")))
            .AdditionalProperties(true)
            .Description(description);

    private static JsonSchemaBuilder IvrFlow()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Properties(
                ("RootNodeId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier of the node the caller hears first.")),
                ("MaxRetries", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("How many times a caller may fail to choose before the fallback action takes over.")),
                ("FallbackAction", IvrAction("What happens when the retries run out.")),
                ("Nodes", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("NodeId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Identifier other nodes and the flow refer to.")),
                            ("Prompt", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The spoken prompt.")),
                            ("PromptMediaId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Voice media played instead of speaking the prompt, when configured.")),
                            ("Options", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Digit", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The telephone key the caller presses (0-9, * or #).")),
                                        ("Action", IvrAction("What pressing the key does.")))
                                    .AdditionalProperties(true))
                                .Description("The keys this menu accepts.")))
                        .AdditionalProperties(true))
                    .Description("The menus of the flow.")))
            .AdditionalProperties(true)
            .Description("Optional menu callers hear before they are routed. An entry point with no flow, or a flow with no nodes, routes straight to its target. The flow is validated as a runnable menu tree on import.");
}
