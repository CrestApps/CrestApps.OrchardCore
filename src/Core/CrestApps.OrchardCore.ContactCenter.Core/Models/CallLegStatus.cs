namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the normalized lifecycle state of a single call leg.
/// </summary>
public enum CallLegStatus
{
    /// <summary>
    /// The provider has not reported a state for the leg.
    /// </summary>
    Unknown,

    /// <summary>
    /// The leg is being originated toward its destination.
    /// </summary>
    Dialing,

    /// <summary>
    /// The destination is alerting.
    /// </summary>
    Ringing,

    /// <summary>
    /// The leg is answered and carrying media.
    /// </summary>
    Answered,

    /// <summary>
    /// The leg is answered but its media is suspended.
    /// </summary>
    OnHold,

    /// <summary>
    /// The leg has cleared normally.
    /// </summary>
    Ended,

    /// <summary>
    /// The leg terminated without ever being answered.
    /// </summary>
    Failed,
}
