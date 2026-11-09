using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.ContactCenter.Recipes.Workflows;

/// <summary>
/// Describes the recipe schema of the <c>ScheduleDialerRetryTask</c> workflow task.
/// </summary>
public sealed class ScheduleDialerRetryTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = nameof(ScheduleDialerRetryTask);

    /// <inheritdoc />
    protected override string Category => "Contact Center";

    /// <inheritdoc />
    protected override string DisplayText => "Schedule Dialer Retry";

    /// <inheritdoc />
    protected override string Description => "Creates the next attempt of a completed dialer activity and queues it in its campaign, within the dialer profile's attempt limit, and writes NextActivityItemId, NextAttemptNumber and NextAttemptDueUtc to the workflow output when it is scheduled";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Scheduled", "Exhausted", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(ScheduleDialerRetryTask.ActivityItemId), WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the ItemId of the activity to try again. Defaults to '{{ Workflow.Input.Data.ActivityItemId }}'."));
        yield return (nameof(ScheduleDialerRetryTask.DelayMinutes), WorkflowActivitySchemaBuilders.String("The optional Liquid expression that resolves the delay, in whole minutes, before the next attempt. When it is empty, the dialer profile's retry delay is used, and the attempt is never due sooner than that delay. A value that is not a whole number of minutes ends the task with Failed."));
    }
}
