using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed class ContactCenterAgentLegFailureService : IContactCenterAgentLegFailureService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly ITelephonyService _telephonyService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentLegFailureService"/> class.
    /// </summary>
    public ContactCenterAgentLegFailureService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        ITelephonyService telephonyService,
        IClock clock,
        ILogger<ContactCenterAgentLegFailureService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _clock = clock;
        _logger = logger;
        _telephonyService = telephonyService;
    }

    /// <inheritdoc />
    public async Task<bool> FailAsync(
        string providerName,
        string peerProviderCallId,
        HangupCause? hangupCause,
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

        var now = _clock.UtcNow;
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

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

        // Release the customer. They answered and are connected to an agent who was never reached, so leaving
        // the leg up holds them on dead air and keeps billing the call.
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

        return true;
    }
}
