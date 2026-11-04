using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Approved time off and the published shifts it overlaps.
/// </summary>
public sealed class TimeOffApprovedContext
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public TimeOffRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public WorkerProfile Worker { get; set; }

    /// <summary>
    /// Gets or sets the worker's published shifts the time off overlaps. A handler removes from the list the shifts
    /// it took off the schedule; the rest are flagged on the request.
    /// </summary>
    public IList<CalendarEvent> OverlappingShifts { get; set; } = [];

    /// <summary>
    /// Gets or sets who approved it.
    /// </summary>
    public string Actor { get; set; }
}
