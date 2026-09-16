namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the outbound dialing strategy used by a dialer profile.
/// </summary>
public enum DialerMode
{
    /// <summary>
    /// The agent chooses and places the call manually.
    /// </summary>
    Manual,

    /// <summary>
    /// The agent reviews the activity, then accepts or skips before dialing.
    /// </summary>
    Preview,

    /// <summary>
    /// The system reserves agents and dials a controlled number of calls per available agent.
    /// </summary>
    Power,

    /// <summary>
    /// The system dials one call per reserved agent as agents become available.
    /// </summary>
    Progressive,

    /// <summary>
    /// The system forecasts answer rates and dials ahead of agent availability.
    /// </summary>
    Predictive,
}
