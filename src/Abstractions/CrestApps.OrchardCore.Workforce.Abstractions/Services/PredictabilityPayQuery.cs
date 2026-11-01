using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Which predictability pay entries to list.
/// </summary>
public sealed class PredictabilityPayQuery
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
    public PredictabilityPayStatus? Status { get; set; }
}
