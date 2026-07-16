namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies why the outbound dialer compliance gate suppressed a dialing attempt.
/// </summary>
public enum DialerSuppressionReason
{
    /// <summary>
    /// The attempt is eligible and was not suppressed.
    /// </summary>
    None,

    /// <summary>
    /// The activity has no preferred destination to dial.
    /// </summary>
    NoDestination,

    /// <summary>
    /// The activity already reached the configured maximum number of attempts.
    /// </summary>
    MaxAttemptsReached,

    /// <summary>
    /// A previous attempt is still within the configured retry cool-down window.
    /// </summary>
    RetryCoolDown,

    /// <summary>
    /// The contact opted out of phone calls through its communication preferences.
    /// </summary>
    DoNotCall,

    /// <summary>
    /// The destination is listed on a national do-not-call registry.
    /// </summary>
    NationalDoNotCallRegistry,

    /// <summary>
    /// The contact's local time is outside the configured calling window.
    /// </summary>
    OutsideCallingWindow,

    /// <summary>
    /// The dialer profile's rolling abandonment rate reached or exceeded its configured cap, or the
    /// abandonment statistics required to prove compliance were unavailable.
    /// </summary>
    AbandonmentRateExceeded,
}
