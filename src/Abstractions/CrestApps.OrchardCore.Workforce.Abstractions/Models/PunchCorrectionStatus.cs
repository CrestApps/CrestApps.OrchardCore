namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a worker's punch correction stands.
/// </summary>
public enum PunchCorrectionStatus
{
    /// <summary>
    /// Waiting for a manager.
    /// </summary>
    Pending,

    /// <summary>
    /// Approved; the punch was added.
    /// </summary>
    Approved,

    /// <summary>
    /// Declined.
    /// </summary>
    Declined,
}
