using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The supervisor interventions beyond listening, whispering and barging: taking a call over, ending it, transferring
/// it, turning its recording on or off, setting an agent's state and messaging an agent. Every one is authorized against
/// the supervisor's queue scope and audited under the supervisor's name.
/// </summary>
public interface IContactCenterSupervisorInterventionService
{
    /// <summary>
    /// Takes a live call over from its agent. The supervisor must already be on it (listening, whispering or barging):
    /// they become heard by the customer, the agent's leg is released, and the supervisor becomes the agent handling
    /// the interaction -- its ownership, their talk time and its reporting move to them, and the released agent goes
    /// to after-call work or back to ready under the normal end-of-work rules.
    /// </summary>
    /// <param name="interactionId">The interaction.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> TakeOverAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a live call for everybody on it.
    /// </summary>
    /// <param name="interactionId">The interaction.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> EndCallAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Blind-transfers another agent's live call to a queue, an agent or a number, through the Contact Center's own
    /// transfer. Every supervisor engaged on the call is released first, since none of them can follow it.
    /// </summary>
    /// <param name="interactionId">The interaction.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="targetType">The kind of destination.</param>
    /// <param name="targetId">The queue id, agent id or number.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> TransferAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        InteractionTransferTargetType targetType,
        string targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a live call's recording on or off. Refused while a sensitive-data capture has recording paused.
    /// </summary>
    /// <param name="interactionId">The interaction.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="record"><see langword="true"/> to record, <see langword="false"/> to stop.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> SetRecordingAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        bool record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an agent's state on their behalf, under the same presence rules the agent's own change follows: an agent on
    /// a call gets the state when their work ends.
    /// </summary>
    /// <param name="agentId">The agent profile.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="status">Available, Away (not ready) or Break.</param>
    /// <param name="reason">The reason code or name, when one applies.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> SetAgentStateAsync(
        string agentId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        AgentPresenceStatus status,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs an agent out of their queues and campaigns.
    /// </summary>
    /// <param name="agentId">The agent profile.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> SignOutAgentAsync(
        string agentId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an agent a short message, shown on their soft phone.
    /// </summary>
    /// <param name="agentId">The agent profile.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="supervisorName">The name the agent sees it from.</param>
    /// <param name="principal">The supervisor's principal.</param>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    Task<SupervisorEngagementResult> SendMessageAsync(
        string agentId,
        string supervisorUserId,
        string supervisorName,
        ClaimsPrincipal principal,
        string text,
        CancellationToken cancellationToken = default);
}
