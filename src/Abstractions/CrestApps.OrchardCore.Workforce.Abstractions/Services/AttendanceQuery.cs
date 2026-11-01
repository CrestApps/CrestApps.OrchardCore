using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which attendance exceptions to list.
/// </summary>
public sealed class AttendanceQuery
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
    /// Gets or sets the status; <see langword="null"/> for every status.
    /// </summary>
    public AttendanceExceptionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the kind; <see langword="null"/> for every kind.
    /// </summary>
    public AttendanceExceptionKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? To { get; set; }
}
