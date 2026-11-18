using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.BusinessHours.Workflows;

/// <summary>
/// Describes the Check Business Hours workflow task to CrestApps Recipes.
/// </summary>
internal sealed class CheckBusinessHoursTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(CheckBusinessHoursTask);

    protected override string DisplayText => "Check Business Hours";

    protected override string Description => "Checks whether a business hours schedule is open now";

    protected override string Category => "Business Hours";

    protected override IEnumerable<string> Outcomes => [CheckBusinessHoursTask.OpenOutcome, CheckBusinessHoursTask.ClosedOutcome, CheckBusinessHoursTask.UnknownOutcome];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(CheckBusinessHoursTask.ScheduleId), WorkflowActivitySchemaBuilders.String("The id of the business hours schedule to check: a Business Hours schedule or another schedule source, such as a Contact Center business hours calendar."));
        yield return (nameof(CheckBusinessHoursTask.TimeZoneExpression), WorkflowActivitySchemaBuilders.String("A Liquid expression that gives a time zone to read the schedule in instead of its own, such as the contact's. Optional."));
    }
}
