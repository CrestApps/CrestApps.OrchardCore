namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where an attendance exception stands.
/// </summary>
public enum AttendanceExceptionStatus
{
    /// <summary>
    /// Waiting for a manager.
    /// </summary>
    Open,

    /// <summary>
    /// A manager excused it; it earns no points.
    /// </summary>
    Excused,

    /// <summary>
    /// A manager confirmed it.
    /// </summary>
    Confirmed,
}
