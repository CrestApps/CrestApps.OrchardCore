namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// What happened to an activity's place in the dialer when it was moved to another campaign.
/// </summary>
public enum ActivityDialerCampaignMoveResult
{
    /// <summary>
    /// The activity was not waiting in any campaign's dialer queue, so there was nothing to move.
    /// </summary>
    NotQueued,

    /// <summary>
    /// The activity was already waiting in the target campaign's queue.
    /// </summary>
    AlreadyThere,

    /// <summary>
    /// The activity was taken out of its old campaign's queue and now waits in the target campaign's queue.
    /// </summary>
    Moved,

    /// <summary>
    /// The dialer is working the activity right now (dialing it, or an agent has it), so it was left where it is.
    /// </summary>
    InFlight,
}
