using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The agent's leg of a joined call hanging up first.
/// </summary>
public sealed partial class ContactCenterAgentLegFailureService
{
    // Who ended the call, in the terms the provider's own hangup metadata uses for the party that hung up.
    private const string AgentHangupSource = "agent";

    /// <inheritdoc />
    public async Task<bool> RecordEndedAsync(
        string providerName,
        string peerProviderCallId,
        string agentLegProviderCallId,
        DateTime? endedUtc,
        HangupCause? hangupCause,
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

        // A settled call has already ended; this is the teardown of its agent leg.
        if (interaction is null || interaction.IsSettled)
        {
            return false;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is null ||
            CallSessionLifecycle.IsTerminal(session.State) ||
            !CarriesTheCall(session, agentLegProviderCallId))
        {
            return false;
        }

        var endedAt = endedUtc ?? _clock.UtcNow;

        // The call ends through provider truth like any other ending, so its talk and hold time, its cause and the
        // agent's wrap-up are decided in one place. It is dated when the agent's leg hung up rather than when this
        // delivery happened to be processed, and it names the agent's leg so that leg, not the caller's, is the one
        // recorded as having hung up.
        var ended = await _providerVoiceEventService.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = providerName,
            ProviderCallId = peerProviderCallId,
            ProviderLegId = agentLegProviderCallId,
            State = VoiceCallState.Ended,
            HangupCause = hangupCause ?? HangupCause.NormalClearing,
            OccurredUtc = endedAt,
            IdempotencyKey = $"agent-leg-ended:{agentLegProviderCallId}",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ContactCenterConstants.TelephonyMetadata.HangupSource] = AgentHangupSource,
            },
        }, cancellationToken);

        if (ended is null)
        {
            return false;
        }

        // The provider does not always release a bridged caller when the other leg goes. Left up, the caller sits on
        // a silent line that is still billed.
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
            // The call is already recorded as ended; a hangup that does not land must not undo that.
            _logger.LogError(
                ex,
                "An error occurred while releasing the customer leg of call '{ProviderCallId}' after its agent hung up.",
                peerProviderCallId.SanitizeLogValue());
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The agent leg '{AgentLegId}' of call '{ProviderCallId}' hung up first; the call was ended at {EndedUtc:O} and the customer released.",
                agentLegProviderCallId.SanitizeLogValue(),
                peerProviderCallId.SanitizeLogValue(),
                endedAt);
        }

        return true;
    }

    // Whether the agent leg is still what joins an agent to the caller: it was answered and joined, it has not ended,
    // it belongs to the agent the call is with, and no other agent is on the call. A leg the call has moved on from
    // (a transfer to another agent) leaving does not end the call.
    private static bool CarriesTheCall(CallSession session, string agentLegProviderCallId)
    {
        var leg = session.Legs.FirstOrDefault(candidate =>
            candidate is not null &&
            candidate.Role == CallPartyRole.Agent &&
            string.Equals(candidate.ProviderLegId, agentLegProviderCallId, StringComparison.Ordinal));

        if (leg is null || !leg.AnsweredUtc.HasValue || leg.EndedUtc.HasValue)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(leg.AgentId) &&
            !string.IsNullOrEmpty(session.AgentId) &&
            !string.Equals(leg.AgentId, session.AgentId, StringComparison.Ordinal))
        {
            return false;
        }

        return !session.Legs.Any(other =>
            other is not null &&
            !string.Equals(other.ProviderLegId, agentLegProviderCallId, StringComparison.Ordinal) &&
            other.Role == CallPartyRole.Agent &&
            other.AnsweredUtc.HasValue &&
            !other.EndedUtc.HasValue);
    }
}
