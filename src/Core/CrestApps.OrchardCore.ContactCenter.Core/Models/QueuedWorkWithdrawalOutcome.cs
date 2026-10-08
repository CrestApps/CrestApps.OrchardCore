namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What happened when an activity's queued work was asked to leave its queue.
/// </summary>
public enum QueuedWorkWithdrawalOutcome
{
    /// <summary>
    /// The activity had no work in a queue, so there was nothing to withdraw.
    /// </summary>
    NothingQueued,

    /// <summary>
    /// The work was waiting and has been removed from its queue.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// The work was ringing an agent; the offer was revoked, the agent released, and the work removed.
    /// </summary>
    OfferRevoked,

    /// <summary>
    /// An agent has already taken the work, so it was left with them: withdrawing it would pull a live call or
    /// open record out from under them. Their own completion settles it.
    /// </summary>
    LeftWithAgent,

    /// <summary>
    /// The work could not be withdrawn right now, because routing held its queue or the offer was being settled
    /// concurrently. Routing withdraws it on its next pass instead.
    /// </summary>
    Deferred,
}
