using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentCallHoldRecorder"/>.
/// </summary>
/// <remarks>
/// The hold arithmetic is the provider stream's own (<see cref="CallSessionHolds"/>), so a hold is measured the same
/// way whoever reported it, and the call ending closes a hold that is still running just as it does for a provider
/// hold. The change is written under the call's ingestion lease, so it cannot interleave with the provider's events.
/// </remarks>
public sealed class AgentCallHoldRecorder : IAgentCallHoldRecorder
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IVoiceIngressGate _ingressGate;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCallHoldRecorder"/> class.
    /// </summary>
    /// <param name="callSessionManager">The call session manager.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="agentManager">The agent profile manager used to confirm the call is the agent's.</param>
    /// <param name="auditRecorder">The recorder that writes the hold to the audit log.</param>
    /// <param name="ingressGate">The gate that serializes every change to one call stream.</param>
    /// <param name="session">The YesSql session the change is committed through.</param>
    /// <param name="logger">The logger.</param>
    public AgentCallHoldRecorder(
        ICallSessionManager callSessionManager,
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        IContactCenterAuditRecorder auditRecorder,
        IVoiceIngressGate ingressGate,
        ISession session,
        ILogger<AgentCallHoldRecorder> logger)
    {
        _callSessionManager = callSessionManager;
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _auditRecorder = auditRecorder;
        _ingressGate = ingressGate;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RecordAsync(TelephonyCallHoldChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (string.IsNullOrEmpty(change.CallId) || string.IsNullOrEmpty(change.UserId) || change.ChangedUtc == default)
        {
            return false;
        }

        await using var lease = await _ingressGate.AcquireAsync(change.ProviderName, change.CallId, cancellationToken);

        var session = await FindSessionAsync(change, cancellationToken);

        // Only a call that is up can be held: a hold on a call nobody answered would be counted as time the customer
        // waited on a call they were never on.
        if (session is null ||
            string.IsNullOrEmpty(session.AgentId) ||
            session.State is not (VoiceCallState.Connected or VoiceCallState.OnHold))
        {
            return false;
        }

        var agent = await _agentManager.FindByUserIdAsync(change.UserId, cancellationToken);

        if (agent is null || !string.Equals(agent.ItemId, session.AgentId, StringComparison.Ordinal))
        {
            return false;
        }

        // A retried command finds the call already in the state it asks for, and records nothing.
        if (session.IsOnHold == change.IsOnHold)
        {
            return false;
        }

        var interaction = string.IsNullOrEmpty(session.InteractionId)
            ? null
            : await _interactionManager.FindByIdAsync(session.InteractionId, cancellationToken);

        var now = DateTime.SpecifyKind(change.ChangedUtc, DateTimeKind.Utc);
        var previousState = session.State;
        double? lasted = null;
        var holdStartedUtc = session.HoldStartedUtc;

        if (change.IsOnHold)
        {
            CallSessionHolds.Start(session, now, placedByAgent: true);
            holdStartedUtc = session.HoldStartedUtc;
            Move(session, interaction, VoiceCallState.OnHold, InteractionStatus.Held);
        }
        else
        {
            lasted = CallSessionHolds.End(session, now);
            Move(session, interaction, VoiceCallState.Connected, InteractionStatus.Connected);
        }

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        if (interaction is not null)
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        var eventType = change.IsOnHold
            ? ContactCenterConstants.Events.CallHeld
            : ContactCenterConstants.Events.CallResumed;

        var data = ContactCenterCallAudit.ForSession(session, interaction);
        data.PreviousState = previousState.ToString();
        data.DurationSeconds = lasted;
        data.Details["source"] = "softPhone";
        data.Details["userId"] = change.UserId;

        if (holdStartedUtc.HasValue)
        {
            data.Details["holdStartedUtc"] = holdStartedUtc.Value.ToString("O", CultureInfo.InvariantCulture);
        }

        // Keyed on the hold itself: the hold and its resume are one change each, however often the agent's phone
        // sends them.
        await _auditRecorder.RecordCallAsync(
            eventType,
            data,
            now,
            ContactCenterActor.Agent(change.UserId),
            $"agent-hold:{eventType}:{session.ItemId}:{(holdStartedUtc ?? now).Ticks.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);

        await _session.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Recorded the agent's {EventType} on call session '{CallSessionId}' for call '{CallId}'. HoldSeconds={HoldSeconds}.",
                eventType,
                session.ItemId.SanitizeLogValue(),
                change.CallId.SanitizeLogValue(),
                session.HoldSeconds);
        }

        return true;
    }

    private async Task<CallSession> FindSessionAsync(TelephonyCallHoldChange change, CancellationToken cancellationToken)
    {
        // The interaction named by the user's own call history is the trusted link; the call id is the fallback for a
        // call the history recorded before it knew the interaction.
        if (!string.IsNullOrEmpty(change.InteractionId))
        {
            var byInteraction = await _callSessionManager.FindByInteractionIdAsync(change.InteractionId, cancellationToken);

            if (byInteraction is not null)
            {
                return byInteraction;
            }
        }

        var byCall = !string.IsNullOrEmpty(change.ProviderName)
            ? await _callSessionManager.FindByProviderCallIdAsync(change.ProviderName, change.CallId, cancellationToken)
            : null;

        return byCall ?? await _callSessionManager.FindByProviderCallIdAsync(change.CallId, cancellationToken);
    }

    private static void Move(CallSession session, Interaction interaction, VoiceCallState state, InteractionStatus status)
    {
        if (session.State != state && session.CanTransitionTo(state))
        {
            session.TransitionTo(state);
        }

        if (interaction is not null && interaction.Status != status && interaction.CanTransitionTo(status))
        {
            interaction.TransitionTo(status);
        }
    }
}
