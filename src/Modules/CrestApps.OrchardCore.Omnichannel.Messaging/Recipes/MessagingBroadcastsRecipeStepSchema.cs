using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Describes the <c>MessagingBroadcasts</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MessagingBroadcastsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => MessagingBroadcastsStep.StepKey;

    protected override string EntriesName => MessagingBroadcastsStep.EntriesName;

    protected override string Description => "Creates or updates messaging broadcasts, matched by ItemId. A new broadcast is always a draft, and a broadcast that is queued or sent is left as it is, so a recipe never sends a message.";

    protected override string EntryDescription => "A message to send to many recipients from one address.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "Channel", "ServiceAddress", "Body", "Recipients"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the broadcast.")),
            ("Channel", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The channel the broadcast is sent on, such as 'sms'.")),
            ("ServiceAddress", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The address the broadcast is sent from, such as the business phone number.")),
            ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The message.")),
            ("Recipients", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The addresses the message goes to.")),
            ("OwnerAgentId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The user id of the agent who owns the replies.")),
        ];
}
