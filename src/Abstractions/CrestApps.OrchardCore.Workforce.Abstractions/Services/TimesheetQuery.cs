using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which timesheets to list.
/// </summary>
public sealed class TimesheetQuery
{
    /// <summary>
    /// Gets or sets the teams; <see langword="null"/> for every team.
    /// </summary>
    public IReadOnlyCollection<string> TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the statuses; empty for every status.
    /// </summary>
    public IReadOnlyCollection<TimesheetStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the pay periods.
    /// </summary>
    public string PayPeriodCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period.
    /// </summary>
    public DateOnly? PeriodStart { get; set; }
}
