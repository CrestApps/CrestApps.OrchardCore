using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed partial class ContactCenterAgentLegFailureService : IContactCenterAgentLegFailureService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly ITelephonyService _telephonyService;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IDialerAbandonmentTracker _abandonmentTracker;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityReservationService _reservationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentLegFailureService"/> class.
    /// </summary>
    /// <remarks>
    /// The abandonment tracker plays an automated dialer profile's abandoned-call message to a person whose agent never
    /// connected, and records an agent who connected too late. The reservation manager and service find and release
    /// the accepted offer of an inbound caller in a queue, whose fate is the queue's unanswered-offer action rather
    /// than a failed call.
    /// </remarks>
    public ContactCenterAgentLegFailureService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        ITelephonyService telephonyService,
        IContactCenterAuditRecorder auditRecorder,
        IProviderVoiceEventService providerVoiceEventService,
        IClock clock,
        ILogger<ContactCenterAgentLegFailureService> logger,
        IAgentPresenceManager presenceManager,
        IDialerAbandonmentTracker abandonmentTracker,
        IActivityReservationManager reservationManager,
        IActivityReservationService reservationService)
    {
        _presenceManager = presenceManager;
        _abandonmentTracker = abandonmentTracker;
        _reservationManager = reservationManager;
        _reservationService = reservationService;
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _clock = clock;
        _logger = logger;
        _telephonyService = telephonyService;
        _auditRecorder = auditRecorder;
        _providerVoiceEventService = providerVoiceEventService;
    }

    /// <inheritdoc />
    public Task<bool> FailAsync(
        string providerName,
        string peerProviderCallId,
        HangupCause? hangupCause,
        CancellationToken cancellationToken = default)
        => FailAsync(providerName, peerProviderCallId, hangupCause, DialerAbandonment.Reasons.AgentLegFailed, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> FailAsync(
        string providerName,
        string peerProviderCallId,
        HangupCause? hangupCause,
        string abandonmentReason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(peerProviderCallId))
        {
            return false;
        }

        var interaction = string.IsNullOrWhiteSpace(providerName)
            ? await _interactionManager.FindByProviderInteractionIdAsync(peerProviderCallId, cancellationToken)
            : await _interactionManager.FindByProviderInteractionIdAsync(providerName, peerProviderCallId, cancellationToken);

        // A settled interaction has already recorded its outcome. The agent leg of a call that ended normally
        // also terminates, and treating that as a failure would overwrite the real ending with an artifact of
        // the teardown.
        if (interaction is null || interaction.IsSettled)
        {
            return false;
        }

        // An inbound caller a queue offered is the queue's to decide for. Settling their call as failed hung up on a
        // caller who rang in and waited, and left the agent whose phone could not ring Available for the next caller;
        // the queue's unanswered-offer action applies instead, and the agent is set Away.
        if (IsQueuedInboundCaller(interaction))
        {
            return await ReleaseQueuedCallerAsync(interaction, providerName, peerProviderCallId, hangupCause, cancellationToken);
        }

        // A person who answered an automated dialer call is listening to silence while the agent never arrives. The
        // abandoned-call message starts first, before anything is written, and the provider ends the call once it has
        // been spoken; the failure is then recorded as before.
        var messageStarted = await _abandonmentTracker.AbandonAsync(interaction, providerName, peerProviderCallId, string.IsNullOrEmpty(abandonmentReason) ? DialerAbandonment.Reasons.AgentLegFailed : abandonmentReason, cancellationToken);

        var now = _clock.UtcNow;
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        // Read before the legs are ended below: the agent leg still being connected is the one that failed.
        var agentLeg = session?.Legs.LastOrDefault(leg => leg.Role == CallPartyRole.Agent && !leg.EndedUtc.HasValue);

        if (session is not null && !CallSessionLifecycle.IsTerminal(session.State))
        {
            // Every leg ends with the call. The agent leg is already gone and the customer leg is about to be
            // hung up, so a leg left open here would keep the bridge claiming a party that is not on the call.
            CallTopologyProjector.EndRemainingLegs(session, now);
            CallTopologyProjector.EndRemainingMonitorSessions(session, now);
            CallTopologyProjector.DestroyBridge(session, now);

            session.TransitionTo(VoiceCallState.Ended);
            session.EndedUtc = now;

            await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        interaction.TransitionTo(InteractionStatus.Failed);
        interaction.EndedUtc = now;

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var data = session is not null
            ? ContactCenterCallAudit.ForSession(session, interaction)
            : ContactCenterCallAudit.ForInteraction(interaction);
        data.ProviderLegId = agentLeg?.ProviderLegId;
        data.LegRole = nameof(CallPartyRole.Agent);
        data.AgentId = agentLeg?.AgentId ?? data.AgentId;
        data.State = nameof(CallLegStatus.Failed);
        data.HangupCause = hangupCause?.ToString();
        data.Reason = CallLifecycleReasons.AgentLegFailed;

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegFailed,
            data,
            now,
            ContactCenterActor.Provider(providerName),
            $"agent-leg:{ContactCenterConstants.Events.AgentLegFailed}:{agentLeg?.ProviderLegId ?? peerProviderCallId}",
            cancellationToken);

        // The agent was never reached, so they were never on the call: they go back to work rather than being left Busy
        // with nothing on the line, offered no further work. No wrap-up either: there is nothing to disposition.
        var agentId = agentLeg?.AgentId ?? session?.AgentId ?? interaction.AgentId;

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            await _presenceManager.CompleteWorkAsync(agentId, new AgentStateChangeContext
            {
                InteractionId = interaction.ItemId,
                ChangedUtc = now,
            }, cancellationToken);
        }

        // Release the customer. They answered and are connected to an agent who was never reached, so leaving
        // the leg up holds them on dead air and keeps billing the call. A customer hearing the abandoned-call message
        // is released by the provider when the message ends.
        if (messageStarted)
        {
            _logger.LogWarning(
                "The agent leg of call '{ProviderCallId}' failed with cause {HangupCause}; the call was settled as failed and the customer hears the abandoned-call message before it ends.",
                peerProviderCallId.SanitizeLogValue(),
                hangupCause);

            return true;
        }

        try
        {
            await _telephonyService.HangupAsync(new CallReference
            {
                CallId = peerProviderCallId,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The call is already recorded as failed; a hangup that does not land must not undo that.
            _logger.LogError(
                ex,
                "An error occurred while releasing the customer leg of call '{ProviderCallId}' after its agent leg failed.",
                peerProviderCallId.SanitizeLogValue());
        }

        _logger.LogWarning(
            "The agent leg of call '{ProviderCallId}' failed with cause {HangupCause}; the call was settled as failed and the customer released.",
            peerProviderCallId.SanitizeLogValue(),
            hangupCause);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RecordAnsweredAsync(
        string providerName,
        string peerProviderCallId,
        string agentLegProviderCallId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(peerProviderCallId) ||
            string.IsNullOrWhiteSpace(agentLegProviderCallId))
        {
            return false;
        }

        var interaction = string.IsNullOrWhiteSpace(providerName)
            ? await _interactionManager.FindByProviderInteractionIdAsync(peerProviderCallId, cancellationToken)
            : await _interactionManager.FindByProviderInteractionIdAsync(providerName, peerProviderCallId, cancellationToken);

        // A settled interaction has already recorded its outcome. An agent leg answer arriving after the call was
        // settled is an artifact of teardown ordering and must not reopen a finished call.
        if (interaction is null || interaction.IsSettled)
        {
            return false;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is null || CallSessionLifecycle.IsTerminal(session.State))
        {
            return false;
        }

        var now = _clock.UtcNow;

        // Advance the agent leg the connect command already recorded (at dialing) to answered, and place it on
        // the call's bridge, so the topology reports that the agent was connected and its talk time is measured.
        // The leg is keyed by the agent-leg call id the provider named; UpsertLeg preserves its recorded Agent
        // role and stamps its answered time. A leg that is already answered is left as it is.
        CallTopologyProjector.UpsertLeg(
            session,
            agentLegProviderCallId,
            CallPartyRole.Agent,
            CallLegStatus.Answered,
            now,
            agentId: session.AgentId);

        CallTopologyProjector.EnsureBridge(session, session.Bridge?.ProviderBridgeId, now);
        CallTopologyProjector.Join(session, agentLegProviderCallId, CallPartyRole.Agent, now, agentId: session.AgentId);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        // The agent is on the call: from here a campaign call is the agent's to work and wrap up. An agent who reached
        // the person who answered later than the abandonment threshold still abandoned the call.
        if (DialerCallMetadata.MarkAgentJoined(interaction, now))
        {
            await _abandonmentTracker.RecordAgentConnectedAsync(interaction, now, cancellationToken);
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        // Held Reserved while the dialer reached the customer, the agent is on the call from now.
        await _presenceManager.ConnectHeldAgentAsync(session.AgentId, interaction, now, cancellationToken);

        var data = ContactCenterCallAudit.ForSession(session, interaction);
        data.ProviderLegId = agentLegProviderCallId;
        data.LegRole = nameof(CallPartyRole.Agent);
        data.State = nameof(CallLegStatus.Answered);

        // Keyed on the leg alone, like the answer the connect command records, so the two are one record.
        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.AgentLegAnswered,
            data,
            now,
            ContactCenterActor.Provider(providerName),
            $"agent-leg:{ContactCenterConstants.Events.AgentLegAnswered}:{agentLegProviderCallId}",
            cancellationToken);

        return true;
    }
}
