using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The one place an agent's presence state is changed, so no code path can move an agent without the move being
/// recorded for audit and payroll.
/// </summary>
/// <remarks>
/// Every writer of <see cref="AgentProfile.PresenceStatus"/> goes through <see cref="TransitionAsync"/>: sign-in and
/// sign-out, the agent, a workflow or a supervisor setting a state, routing reserving, accepting and releasing,
/// wrap-up starting and ending, the stale-session sweep and provider reconciliation. The transition is recorded
/// as <see cref="ContactCenterConstants.Events.AgentStateChanged"/> through <see cref="IContactCenterAuditRecorder"/>.
/// It adds to, and never replaces, the presence events writers already publish, which drive routing and real-time
/// broadcasts.
/// </remarks>
public interface IAgentStateTransitionService
{
    /// <summary>
    /// Moves the agent to <paramref name="state"/>, stamps when the state changed, and records the transition.
    /// </summary>
    /// <remarks>
    /// The writer sets everything else the transition changes on the profile (the requested state, the active
    /// reservation, the reason) before calling, so the record describes the profile as the change leaves it, and
    /// saves the profile afterwards. Nothing is recorded when the state does not change, unless
    /// <see cref="AgentStateChangeContext.RecordWhenUnchanged"/> asks for it. The time never goes backwards: a
    /// change dated before the agent's previous one takes the previous one's time, so a day's transitions stay in
    /// order and contiguous.
    /// </remarks>
    /// <param name="profile">The agent, with its current state still the state being left.</param>
    /// <param name="state">The state the agent moves to.</param>
    /// <param name="context">Who made the change, why, and what it was about.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The recorded transition, or <see langword="null"/> when nothing was recorded.</returns>
    Task<AgentStateChangedEventData> TransitionAsync(
        AgentProfile profile,
        AgentPresenceStatus state,
        AgentStateChangeContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a reason given for a state against the configured reason codes, by identifier or by name.
    /// </summary>
    /// <param name="reason">A reason code identifier, a reason code name, or free text.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The resolved reason, or <see langword="null"/> when no reason was given.</returns>
    Task<AgentStateReason> ResolveReasonAsync(string reason, CancellationToken cancellationToken = default);
}
