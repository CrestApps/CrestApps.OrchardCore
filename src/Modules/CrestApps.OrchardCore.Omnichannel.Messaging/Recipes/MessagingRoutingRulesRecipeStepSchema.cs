using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Describes the <c>MessagingRoutingRules</c> recipe step.
/// </summary>
internal sealed class MessagingRoutingRulesRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    private static JsonSchemaBuilder Strings(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description(description);

    /// <inheritdoc/>
    public override string Name => MessagingRoutingRulesStep.StepKey;

    /// <inheritdoc/>
    protected override string EntriesName => MessagingRoutingRulesStep.EntriesName;

    /// <inheritdoc/>
    protected override string Description => "Creates or updates the messaging routing rules, matched by ItemId. A rule assigns a conversation nobody owns to an agent picked from a queue, teams, roles or named users.";

    /// <inheritdoc/>
    protected override string EntryDescription => "A rule that assigns conversations nobody owns to an agent.";

    /// <inheritdoc/>
    protected override IEnumerable<string> RequiredEntryProperties => ["DisplayText", "CandidateSource", "Strategy"];

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The rule's name.")),
            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the rule is for.")),
            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the rule is used. Defaults to true.")),
            ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The rule's place among the rules; a lower number is tried first.")),
            ("Triggers", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("When the rule is tried, a comma-separated list of NewConversation, ContactReplied, StartedWithoutAgent and Waiting, or their flag value.")),
            ("WaitingMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("How many minutes a contact's message waits before the Waiting sweep tries the rule.")),
            ("Channels", Strings("The channels the rule takes, such as 'sms'. Empty takes every channel.")),
            ("ServiceAddresses", Strings("The business addresses the rule takes, normalized for their channel. Empty takes every address.")),
            ("QueueIds", Strings("The queues the rule takes conversations of. Empty takes any queue and none.")),
            ("CampaignIds", Strings("The campaigns the rule takes conversations of. Empty takes any campaign and none.")),
            ("CandidateSource", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("QueueSignedIn", "QueueMembers", "Teams", "Roles", "Users").Description("Where the agents come from.")),
            ("CandidateQueueId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The queue for the QueueSignedIn and QueueMembers sources. Empty uses the conversation's own queue.")),
            ("TeamIds", Strings("The teams for the Teams source.")),
            ("RoleNames", Strings("The roles for the Roles source.")),
            ("UserIds", Strings("The user ids for the Users source.")),
            ("Strategy", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("RoundRobin", "LeastOpenConversations", "LeastLoaded").Description("How one agent is picked.")),
            ("OnlyAvailableAgents", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether only agents available for messaging can be picked. Defaults to true.")),
            ("RespectMaxConcurrent", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether agents at their conversation limit are skipped. Defaults to true.")),
        ];
}
