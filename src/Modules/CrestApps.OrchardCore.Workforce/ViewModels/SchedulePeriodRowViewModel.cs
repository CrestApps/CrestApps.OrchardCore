using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of the schedule list.
/// </summary>
public class SchedulePeriodRowViewModel
{
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public SchedulePeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the name of its team.
    /// </summary>
    public string TeamName { get; set; }
}
