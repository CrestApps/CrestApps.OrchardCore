using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>ScheduleCallbackTask</c> workflow task.
/// </summary>
public sealed class ScheduleCallbackTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ScheduleCallbackTask";

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Schedule Callback";

    /// <inheritdoc />
    protected override string Description => "Schedules a Contact Center callback in response to a domain event.";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Destination", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the destination number or address to call back."));
        yield return ("DelayMinutes", WorkflowActivitySchemaBuilders.Integer("The delay, in minutes from now, before the callback becomes due. Zero schedules it immediately."));
        yield return ("CampaignId", WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the campaign the callback belongs to."));
        yield return ("QueueId", WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the queue the promoted activity is enqueued into."));
        yield return ("ContactContentItemId", WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the content item identifier of the contact."));
    }
}
