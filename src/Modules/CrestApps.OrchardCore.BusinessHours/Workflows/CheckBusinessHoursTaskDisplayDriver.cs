using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.BusinessHours.Workflows;

/// <summary>
/// The editor of the Check Business Hours workflow task.
/// </summary>
public sealed class CheckBusinessHoursTaskDisplayDriver : ActivityDisplayDriver<CheckBusinessHoursTask, CheckBusinessHoursTaskViewModel>
{
    /// <inheritdoc />
    protected override void EditActivity(CheckBusinessHoursTask activity, CheckBusinessHoursTaskViewModel model)
    {
        model.ScheduleId = activity.ScheduleId;
        model.TimeZoneExpression = activity.TimeZoneExpression;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(CheckBusinessHoursTaskViewModel model, CheckBusinessHoursTask activity)
    {
        activity.ScheduleId = model.ScheduleId?.Trim();
        activity.TimeZoneExpression = model.TimeZoneExpression?.Trim();
    }
}
