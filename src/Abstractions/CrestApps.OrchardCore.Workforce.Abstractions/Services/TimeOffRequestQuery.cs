using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which time-off requests to list.
/// </summary>
public sealed class TimeOffRequestQuery
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
    /// Gets or sets the status; <see langword="null"/> for the requests that wait for a place or a decision.
    /// </summary>
    public TimeOffRequestStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the first day of a range the requests overlap.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the last day of a range the requests overlap.
    /// </summary>
    public DateOnly? To { get; set; }
}
