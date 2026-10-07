namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What connecting an answered over-dialed call did.
/// </summary>
public enum PredictiveConnectOutcome
{
    /// <summary>
    /// The call is not an over-dialed call waiting for an agent: not over-dialed, not answered by a person, or over.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// Another delivery already claimed an agent for the call or abandoned it.
    /// </summary>
    AlreadyHandled = 1,

    /// <summary>
    /// Another delivery is connecting the call right now.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// An agent was claimed and is being connected.
    /// </summary>
    Claimed = 3,

    /// <summary>
    /// Nobody was free yet, and the profile's connect wait has not run out; the call is tried again shortly.
    /// </summary>
    Waiting = 4,

    /// <summary>
    /// Nobody was free: the abandoned-call message was started (or the call hung up) and the call counted abandoned.
    /// </summary>
    Abandoned = 5,
}
