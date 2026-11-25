namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// What happened to an activity's place in the dialer when it was taken off the dialer.
/// </summary>
public enum ActivityDialerWithdrawResult
{
    /// <summary>
    /// The activity was not waiting in any campaign's dialer queue, so there was nothing to take off.
    /// </summary>
    NotQueued,

    /// <summary>
    /// The activity was taken out of its campaign's dialer queue, so the dialer no longer calls it.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// The dialer is working the activity right now (dialing it, or an agent has it), so it was left where it is.
    /// </summary>
    InFlight,
}
