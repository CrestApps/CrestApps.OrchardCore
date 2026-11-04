namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Whether a worker can be scheduled.
/// </summary>
public enum WorkerStatus
{
    /// <summary>
    /// Working; can be scheduled.
    /// </summary>
    Active,

    /// <summary>
    /// On extended leave; not scheduled until they return.
    /// </summary>
    OnLeave,

    /// <summary>
    /// Temporarily not allowed to work.
    /// </summary>
    Suspended,

    /// <summary>
    /// No longer works here; kept for history.
    /// </summary>
    Terminated,
}
