namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Names a state an agent is in that nothing live backs, which keeps routing from giving them work or makes them look
/// free when they are not.
/// </summary>
internal enum AgentAvailabilityFlagKind
{
    /// <summary>
    /// The agent is Available, but has no live session, so routing passes them over.
    /// </summary>
    AvailableWithoutLiveSession,

    /// <summary>
    /// A soft-phone call record is still open although the provider no longer has the call, cannot resolve it, or the
    /// browser that placed it is gone.
    /// </summary>
    UnresolvedSoftPhoneCall,

    /// <summary>
    /// A Contact Center interaction is still open although the provider reports its call ended.
    /// </summary>
    OpenInteractionAfterCallEnded,

    /// <summary>
    /// The agent is Busy with no live interaction behind it.
    /// </summary>
    BusyWithoutLiveInteraction,

    /// <summary>
    /// The agent is Reserved with no live offer behind it.
    /// </summary>
    ReservedWithoutLiveInteraction,

    /// <summary>
    /// The agent holds a reservation that no live offer or call backs.
    /// </summary>
    StaleReservation,

    /// <summary>
    /// Work is still reserved for, or assigned to, the agent although no offer or call backs it.
    /// </summary>
    StaleAssignedWork,

    /// <summary>
    /// The agent holds a reservation: an offer may be ringing for them right now.
    /// </summary>
    HoldsReservation,

    /// <summary>
    /// Stale work could not be looked for, because the feature that looks for it is not enabled.
    /// </summary>
    StaleWorkNotChecked,
}
