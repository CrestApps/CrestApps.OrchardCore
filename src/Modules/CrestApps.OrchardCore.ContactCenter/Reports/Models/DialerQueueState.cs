namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Where a pending campaign record stands with the dialer right now. Every explained pending record is in exactly one
/// state, so the states of a campaign's pending records add up to its pending count, with the records that were not
/// explained.
/// </summary>
public enum DialerQueueState
{
    /// <summary>
    /// Nothing holds the record back: the dialer can dial it now.
    /// </summary>
    DialableNow,

    /// <summary>
    /// The record waits for a time set on it, such as a follow-up the subject's "Try again" action created.
    /// </summary>
    ScheduledForLater,

    /// <summary>
    /// The record has no item in any queue, so nothing will ever dial it until it is loaded again.
    /// </summary>
    NotInAnyQueue,

    /// <summary>
    /// The record is assigned to a user, who works it from their own list rather than through the dialer.
    /// </summary>
    AssignedToUser,

    /// <summary>
    /// The record is held back for any other reason, such as a turned-off dialer profile, the retry delay, no attempts
    /// left or a compliance check.
    /// </summary>
    HeldBack,
}
