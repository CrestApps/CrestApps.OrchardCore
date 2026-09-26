using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// One agent state transition, exactly as it happened: the payload of
/// <see cref="ContactCenterConstants.Events.AgentStateChanged"/>.
/// </summary>
/// <remarks>
/// This is the record payroll and workforce reports are built from, so every transition is written, including the
/// ones routing makes (Reserved, Busy and every release) and the ones the platform makes (sweeps, timeouts,
/// reconciliation). A day's transitions for an agent, in order, account for every moment between sign-in and
/// sign-out. It is a separate event from <see cref="ContactCenterConstants.Events.AgentPresenceChanged"/> on
/// purpose: that event drives routing and real-time broadcasts, and the audit must be able to record every
/// transition without offering work or re-broadcasting presence.
/// </remarks>
public sealed class AgentStateChangedEventData
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the user the agent signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the state before the transition.
    /// </summary>
    public AgentPresenceStatus PreviousState { get; set; }

    /// <summary>
    /// Gets or sets the state after the transition.
    /// </summary>
    public AgentPresenceStatus CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the state the agent asked to move to once their current work ends, when one is pending.
    /// </summary>
    public AgentPresenceStatus? RequestedState { get; set; }

    /// <summary>
    /// Gets or sets the reason code chosen for the new state, by identifier, so renaming a code does not rewrite
    /// history.
    /// </summary>
    public string ReasonCodeId { get; set; }

    /// <summary>
    /// Gets or sets the reason as it read at the time: the reason code's name, or the free text given.
    /// </summary>
    public string ReasonName { get; set; }

    /// <summary>
    /// Gets or sets what caused the transition, as one of <see cref="AgentStateChangeSources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the interaction the transition is about, for one routing made.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the reservation the transition is about, for one routing made.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets why a reservation ended without the work being taken, for a
    /// <see cref="AgentStateChangeSources.Released"/> transition: one of <see cref="AgentReleaseReasons"/>.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="ReasonName"/>, which is the reason for the state the agent entered, so a
    /// release that returns an agent to a break they asked for still reports that break's reason.
    /// </remarks>
    public string ReleaseReason { get; set; }

    /// <summary>
    /// Gets or sets the agent session the transition belongs to.
    /// </summary>
    public string AgentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is signed in to after the transition.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the agent is signed in to after the transition.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the transition took effect, at full precision. For a sign-off after lost contact this is
    /// the last moment the agent was heard from, not the moment the platform noticed.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}

/// <summary>
/// What caused an agent state transition.
/// </summary>
public static class AgentStateChangeSources
{
    /// <summary>The agent signed in.</summary>
    public const string SignIn = "SignIn";

    /// <summary>The agent signed out.</summary>
    public const string SignOut = "SignOut";

    /// <summary>The agent, a supervisor or a workflow set the state.</summary>
    public const string SetState = "SetState";

    /// <summary>Routing reserved the agent for an offer.</summary>
    public const string Reserved = "Reserved";

    /// <summary>The agent accepted work and became busy.</summary>
    public const string Accepted = "Accepted";

    /// <summary>A reservation ended without the work being taken: declined, expired, cancelled or compensated.</summary>
    public const string Released = "Released";

    /// <summary>Wrap-up began when the call ended.</summary>
    public const string WrapUpStarted = "WrapUpStarted";

    /// <summary>The agent finished their work, from wrap-up or directly from a call.</summary>
    public const string WorkCompleted = "WorkCompleted";

    /// <summary>Wrap-up ran past its limit and the platform ended it.</summary>
    public const string WrapUpTimedOut = "WrapUpTimedOut";

    /// <summary>The platform reconciled the agent's state with the provider's view of the call.</summary>
    public const string Reconciled = "Reconciled";

    /// <summary>The agent's session went silent and the platform signed them off.</summary>
    public const string SessionExpired = "SessionExpired";

    /// <summary>A pending requested state took effect after work ended.</summary>
    public const string RequestApplied = "RequestApplied";

    /// <summary>The agent answered a colleague's consult on a live call and became busy.</summary>
    public const string ConsultAnswered = "ConsultAnswered";

    /// <summary>A supervisor took a live call over and became busy on it.</summary>
    public const string SupervisorTakeover = "SupervisorTakeover";
}

/// <summary>
/// Why a reservation ended without its work being taken, recorded on a
/// <see cref="AgentStateChangeSources.Released"/> transition.
/// </summary>
public static class AgentReleaseReasons
{
    /// <summary>The offer rang past its deadline unanswered.</summary>
    public const string Expired = "Expired";

    /// <summary>The agent declined the offer.</summary>
    public const string Rejected = "Rejected";

    /// <summary>The offer was withdrawn, for example because the caller hung up while it rang.</summary>
    public const string Canceled = "Canceled";

    /// <summary>Routing undid a reservation it could not complete, such as a dial that failed.</summary>
    public const string Compensated = "Compensated";
}
