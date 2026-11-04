namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One row of the planner board: a worker's shifts, or the open shifts.
/// </summary>
public class ScheduleBoardRowViewModel
{
    /// <summary>
    /// Gets or sets the worker; empty for the open shifts.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the worker is on the team during the week.
    /// </summary>
    public bool OnTeam { get; set; }

    /// <summary>
    /// Gets or sets the hours scheduled in the week, less unpaid time.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets the shifts of each day.
    /// </summary>
    public IDictionary<DateOnly, IList<ScheduleBoardShiftViewModel>> Cells { get; set; } = new Dictionary<DateOnly, IList<ScheduleBoardShiftViewModel>>();
}
