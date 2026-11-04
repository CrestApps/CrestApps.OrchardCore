namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How a worker feels about working at a time.
/// </summary>
public enum AvailabilityPreference
{
    /// <summary>
    /// Can't work then; never scheduled.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Can work then.
    /// </summary>
    Available,

    /// <summary>
    /// Would like to work then.
    /// </summary>
    Preferred,
}
