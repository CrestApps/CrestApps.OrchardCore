namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

/// <summary>
/// Names why a waiting or pending piece of work is not being handed to an agent, or that nothing stands in its way. Each
/// value mirrors a decision the dispatch code already makes, so a supervisor reads the same reason the dispatcher acted on.
/// </summary>
public enum DispatchBlocker
{
    /// <summary>
    /// Nothing holds the record back: it is waiting for an available agent.
    /// </summary>
    DialableNow,

    /// <summary>
    /// The queue is turned off, so assignment skips it.
    /// </summary>
    QueueDisabled,

    /// <summary>
    /// The queue's business hours are closed, so assignment is paused.
    /// </summary>
    BusinessHoursClosed,

    /// <summary>
    /// The record is pending but is not waiting in the campaign's queue, so routing never sees it.
    /// </summary>
    NotQueued,

    /// <summary>
    /// The dialer profile the record is dialed under is turned off.
    /// </summary>
    ProfileDisabled,

    /// <summary>
    /// The record is scheduled for later, such as a follow-up the subject's "Try again" action created.
    /// </summary>
    ScheduledForLater,

    /// <summary>
    /// The record is still inside the dialer profile's retry cool-down after its last call.
    /// </summary>
    RetryCoolDown,

    /// <summary>
    /// The record has used every attempt its dialer profile or subject allows.
    /// </summary>
    AttemptsExhausted,

    /// <summary>
    /// The record has no destination to dial.
    /// </summary>
    NoDestination,

    /// <summary>
    /// The contact opted out of phone calls.
    /// </summary>
    DoNotCall,

    /// <summary>
    /// The destination is on a national do-not-call registry.
    /// </summary>
    NationalDoNotCallRegistry,

    /// <summary>
    /// The contact's local time is outside the dialer profile's calling window.
    /// </summary>
    OutsideCallingWindow,

    /// <summary>
    /// The destination is known not to be in service.
    /// </summary>
    NumberNotInService,

    /// <summary>
    /// A compliance check could not be completed, so the dial was not permitted.
    /// </summary>
    ComplianceScreeningUnavailable,

    /// <summary>
    /// The dialer profile's abandonment rate reached its cap, or the statistics proving compliance were unavailable.
    /// </summary>
    AbandonmentRateExceeded,

    /// <summary>
    /// The record's queue item is stamped with a dialer profile that no longer exists, so the pacer has no profile to dial
    /// it with.
    /// </summary>
    ProfileMissing,

    /// <summary>
    /// The record is not in its campaign's queue because it is assigned to a user, who works it from their own list rather
    /// than through the dialer.
    /// </summary>
    AssignedToUser,
}
