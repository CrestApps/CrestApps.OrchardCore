namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// What made a change to a published schedule, as <see cref="Models.ScheduleChange.Source"/>.
/// </summary>
public static class ScheduleChangeSources
{
    /// <summary>
    /// A republished schedule.
    /// </summary>
    public const string Publish = "Publish";

    /// <summary>
    /// Approved time off taking a shift off the schedule.
    /// </summary>
    public const string TimeOff = "TimeOff";
}
