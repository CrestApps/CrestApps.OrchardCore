namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The verdict on a profile's connect wait.
/// </summary>
public enum PredictiveConnectWaitVerdict
{
    /// <summary>
    /// The profile does not wait.
    /// </summary>
    NoWait,

    /// <summary>
    /// The wait fits within the abandonment threshold once the measured connect time is added.
    /// </summary>
    Allowed,

    /// <summary>
    /// The wait plus the measured connect time exceeds the abandonment threshold.
    /// </summary>
    ExceedsBudget,

    /// <summary>
    /// Too few connects have been measured to know whether any wait is safe.
    /// </summary>
    NoLatencyData,
}
