using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>TransferToAgentTask</c> workflow task.
/// </summary>
public sealed class TransferToAgentTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "TransferToAgentTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Hand Off to Live Agent";

    /// <inheritdoc />
    protected override string Description => "Moves an automated conversation into the human lane: a live call is seated in a queue and offered to an agent, and a text conversation becomes a queue-owned thread.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Connected", "Waiting In Queue", "Callback Scheduled", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ActivityItemId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the CRM activity identifier to transfer."));
        yield return ("QueueId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the target queue identifier. When omitted, the subject flow's configured handoff queue is used."));
        yield return ("Reason", WorkflowActivitySchemaBuilders.String("The Liquid expression resolving a short reason for the escalation, shown to the agent."));
        yield return ("Summary", WorkflowActivitySchemaBuilders.String("The Liquid expression resolving a summary of the conversation so far, so the receiving agent inherits the context."));
    }
}
