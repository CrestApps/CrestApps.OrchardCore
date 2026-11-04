namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of a schedule's change log.
/// </summary>
public class ScheduleChangeRowViewModel
{
    /// <summary>
    /// Gets or sets the change.
    /// </summary>
    public CrestApps.OrchardCore.Workforce.Models.ScheduleChange Change { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the previous worker's name, for a reassignment.
    /// </summary>
    public string PreviousWorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }
}
