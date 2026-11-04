using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which open shifts to list.
/// </summary>
public sealed class OpenShiftQuery
{
    /// <summary>
    /// Gets or sets the teams; <see langword="null"/> for every team.
    /// </summary>
    public IReadOnlyCollection<string> TeamIds { get; set; }

    /// <summary>
    /// Gets or sets the status; <see langword="null"/> for open and claimed ones.
    /// </summary>
    public OpenShiftStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the first day; <see langword="null"/> for no limit.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the period; <see langword="null"/> for any.
    /// </summary>
    public string PeriodId { get; set; }
}
