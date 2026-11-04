using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A time-off request with what a manager needs to decide on it.
/// </summary>
public class TimeOffRequestDetailsViewModel
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
    /// Gets or sets the time-off type.
    /// </summary>
    public TimeOffType Type { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the worker's balance on the first day, or <see langword="null"/> when no policy applies.
    /// </summary>
    public TimeOffBalance Balance { get; set; }

    /// <summary>
    /// Gets or sets the team's other time off and requests on the same days.
    /// </summary>
    public IList<TimeOffRequestRowViewModel> OthersOff { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's published shifts on those days, with whether the request flagged each one.
    /// </summary>
    public IList<(DateTime Start, DateTime End, bool Flagged)> Shifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of who submitted and who decided.
    /// </summary>
    public string SubmittedByName { get; set; }

    /// <summary>
    /// Gets or sets the name of who decided.
    /// </summary>
    public string DecidedByName { get; set; }
}
