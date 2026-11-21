namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Whether a collector accepts responses.
/// </summary>
public enum CollectorStatus
{
    /// <summary>
    /// It accepts responses.
    /// </summary>
    Open,

    /// <summary>
    /// It does not.
    /// </summary>
    Closed,
}
