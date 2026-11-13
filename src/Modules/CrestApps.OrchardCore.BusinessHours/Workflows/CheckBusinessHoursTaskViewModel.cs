namespace CrestApps.OrchardCore.BusinessHours.Workflows;

/// <summary>
/// The editor of the Check Business Hours workflow task.
/// </summary>
public class CheckBusinessHoursTaskViewModel
{
    /// <summary>
    /// Gets or sets the schedule's id.
    /// </summary>
    public string ScheduleId { get; set; }

    /// <summary>
    /// Gets or sets a time zone to read the schedule in, as a Liquid expression.
    /// </summary>
    public string TimeZoneExpression { get; set; }
}
