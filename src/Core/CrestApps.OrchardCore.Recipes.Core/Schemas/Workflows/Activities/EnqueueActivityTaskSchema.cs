using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>EnqueueActivityTask</c> workflow task.
/// </summary>
public sealed class EnqueueActivityTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "EnqueueActivityTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Enqueue Activity";

    /// <inheritdoc />
    protected override string Description => "Enqueues a CRM activity into a Contact Center queue so it can be routed to an agent.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ActivityItemId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the CRM activity identifier to enqueue."));
        yield return ("QueueId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the target queue identifier."));
        yield return ("Priority", WorkflowActivitySchemaBuilders.EnumValue("The optional priority override. When omitted, the queue's default priority is used.", "Lowest", "Low", "Normal", "High", "Highest"));
    }
}
