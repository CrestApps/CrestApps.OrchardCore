using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The planner board of a team schedule, one week at a time.
/// </summary>
public class ScheduleBoardViewModel
{
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public CrestApps.OrchardCore.Workforce.Models.SchedulePeriod Period { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the team's calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the week shown, from 0.
    /// </summary>
    public int Week { get; set; }

    /// <summary>
    /// Gets or sets how many weeks the schedule has.
    /// </summary>
    public int WeekCount { get; set; }

    /// <summary>
    /// Gets or sets the days shown.
    /// </summary>
    public IList<DateOnly> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets a row per worker, then the open shifts.
    /// </summary>
    public IList<ScheduleBoardRowViewModel> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the coverage of each day shown.
    /// </summary>
    public IDictionary<DateOnly, ScheduleDayCoverageViewModel> Coverage { get; set; } = new Dictionary<DateOnly, ScheduleDayCoverageViewModel>();

    /// <summary>
    /// Gets or sets how many draft shifts the schedule has.
    /// </summary>
    public int ShiftCount { get; set; }

    /// <summary>
    /// Gets or sets how many have nobody.
    /// </summary>
    public int OpenCount { get; set; }

    /// <summary>
    /// Gets or sets whether the user may publish.
    /// </summary>
    public bool CanPublish { get; set; }

    /// <summary>
    /// Gets or sets what the last auto-schedule run did.
    /// </summary>
    public CrestApps.OrchardCore.Workforce.Models.ScheduleSolverRun LastRun { get; set; }

    /// <summary>
    /// Gets or sets the team's other schedules to copy from.
    /// </summary>
    public IList<SelectListItem> CopySourceOptions { get; set; } = [];
}
