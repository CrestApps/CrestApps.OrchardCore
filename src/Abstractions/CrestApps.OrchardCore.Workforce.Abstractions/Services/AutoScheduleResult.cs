using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What auto-scheduling a scenario did.
/// </summary>
public sealed class AutoScheduleResult
{
    /// <summary>
    /// Gets or sets why it couldn't run; empty when it ran.
    /// </summary>
    public IList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the run, as stored on the scenario.
    /// </summary>
    public ScheduleSolverRun Run { get; set; }

    /// <summary>
    /// Gets whether it ran.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
