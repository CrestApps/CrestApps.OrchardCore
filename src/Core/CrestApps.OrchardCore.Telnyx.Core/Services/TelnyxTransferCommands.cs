using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Call Control commands that hand a call dialed from the soft phone to somebody else, shared by the provider
/// (which starts, completes and cancels a transfer when the agent asks) and the bridge orchestrator (which finishes one
/// when a leg answers or hangs up).
/// </summary>
/// <remarks>
/// <para>
/// Such a call is the agent's leg bridged, with <c>park_after_unbridge=self</c>, to the party's leg. A transfer rings
/// its destination on a leg of its own and leaves the party where they are -- still with the agent, held -- until that
/// leg answers. Only then is the party bridged to it (<c>POST /calls/{target}/actions/bridge</c>), which takes the
/// party out of the agent's bridge and parks the agent's leg, and the agent's leg is hung up. A destination that never
/// answers therefore leaves the party exactly where the transfer found them.
/// </para>
/// <para>
/// A colleague is handed the call on a leg their browser rings with Answer and Decline and then follows as its own
/// call: it is bridged to the party exactly as the agent's leg was, and its client state names the party, so the
/// colleague can hold, transfer or merge it again. That leg is also recorded in the colleague's call history the moment
/// it is rung, which is what lets their phone act on it at all.
/// </para>
/// </remarks>
public sealed class TelnyxTransferCommands
{
    /// <summary>
    /// How long a transfer or a consult rings its destination before it is given up.
    /// </summary>
    public const int RingTimeoutSeconds = 30;

    /// <summary>
    /// The SIP header that marks a leg rung to a colleague's browser to hand them a call, for a browser SDK that does not
    /// expose the leg's client state.
    /// </summary>
    public const string TransferLegSipHeader = "X-Transfer-Leg";

    private readonly TelnyxApiClient _apiClient;
    private readonly TelnyxOptions _options;
    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxTransferCommands"/> class.
    /// </summary>
    /// <param name="apiClient">The Telnyx API client.</param>
    /// <param name="options">The active Telnyx settings.</param>
    /// <param name="interactionStore">The call history a colleague's transfer leg is recorded in, when there is one.</param>
    /// <param name="clock">The clock, when there is one.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxTransferCommands(
        TelnyxApiClient apiClient,
        TelnyxOptions options,
        ITelephonyInteractionStore interactionStore,
        IClock clock,
        ILogger logger)
    {
        _apiClient = apiClient;
        _options = options;
        _interactionStore = interactionStore;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// The conference a consult with a colleague is held in, named after the agent's consult leg.
    /// </summary>
    /// <param name="consultAgentLegId">The agent's consult leg.</param>
    public static string ConsultConferenceName(string consultAgentLegId)
        => $"consult-{consultAgentLegId}";

    /// <summary>
    /// Rings a transfer's destination on a leg of its own, and records it in the colleague's history when it rings one.
    /// </summary>
    /// <param name="destination">The colleague's SIP address, or the number.</param>
    /// <param name="state">The transfer leg's state (<see cref="TelnyxOutboundBridgeState.TransferLegIntent"/>).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The new leg, or <see langword="null"/> when Telnyx refused it.</returns>
    public async Task<string> RingAsync(string destination, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var toBrowser = destination.StartsWith("sip:", StringComparison.OrdinalIgnoreCase);
        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = destination,
            From = _options.DefaultOutboundCallerId,
            ClientState = state.ToClientStateJson(),
            TimeoutSeconds = RingTimeoutSeconds,
        };

        // A colleague's browser is reached as an internal SIP address, never through the outbound voice profile (which
        // would route it to the PSTN, where it never arrives). It is told whose call it is, and that it is a transfer.
        if (toBrowser)
        {
            if (!string.IsNullOrWhiteSpace(state.PartyNumber))
            {
                originate.AdditionalFields["from_display_name"] = state.PartyNumber;
            }

            originate.AdditionalFields["custom_headers"] = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = TransferLegSipHeader,
                    ["value"] = string.IsNullOrWhiteSpace(state.PartyNumber) ? "1" : state.PartyNumber,
                },
            };
        }
        else if (!string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            originate.AdditionalFields["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        var result = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.CallControlId))
        {
            _logger.LogError(
                "Telnyx refused the leg of a transfer with status code {StatusCode}. Response: {Response}",
                result.StatusCode,
                result.ErrorBody.SanitizeLogValue());

            return null;
        }

        if (toBrowser && !string.IsNullOrWhiteSpace(state.TargetUserId))
        {
            await RecordColleagueCallAsync(result.CallControlId, state, cancellationToken);
        }

        return result.CallControlId;
    }

    /// <summary>
    /// Bridges the party to a destination that answered, points each of the two legs at the other, and releases the
    /// transferring agent's leg.
    /// </summary>
    /// <param name="targetLegId">The destination's leg: a colleague's browser, or an outside party.</param>
    /// <param name="targetIsBrowser">Whether the destination is a colleague's browser.</param>
    /// <param name="partyLegId">The party being handed over.</param>
    /// <param name="agentLegId">The transferring agent's leg of the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the party was bridged to the destination.</returns>
    public async Task<bool> HandOverAsync(
        string targetLegId,
        bool targetIsBrowser,
        string partyLegId,
        string agentLegId,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = partyLegId,
            ["command_id"] = $"xfer-bridge-{targetLegId}",
        };

        // A colleague's leg now carries the call exactly as the agent's did: parked when the party leaves it, so a
        // transfer or a merge from there can still move the party and release it deliberately.
        if (targetIsBrowser)
        {
            body["park_after_unbridge"] = "self";
        }

        var bridge = await _apiClient.PostCallActionAsync(targetLegId, "bridge", body, cancellationToken);

        if (!bridge.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected bridging a transferred party {PartyLeg} to {TargetLeg} with status code {StatusCode}. Response: {Response}",
                partyLegId.SanitizeLogValue(),
                targetLegId.SanitizeLogValue(),
                bridge.StatusCode,
                bridge.ErrorBody.SanitizeLogValue());

            return false;
        }

        if (targetIsBrowser)
        {
            await UpdateStateAsync(targetLegId, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.AgentLegIntent,
                PeerCallControlId = partyLegId,
                TransferOfCallControlId = agentLegId,
            }, cancellationToken);

            await UpdateStateAsync(partyLegId, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = targetLegId,
            }, cancellationToken);
        }
        else
        {
            // Two outside parties, joined and left alone: each releases the other. They still name the agent's leg they
            // came from, which the call history knows, so neither is taken for an orphaned call.
            await UpdateStateAsync(targetLegId, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = agentLegId,
                TransferOfCallControlId = agentLegId,
                ReleaseWithCallControlId = partyLegId,
                Detached = true,
            }, cancellationToken);

            await UpdateStateAsync(partyLegId, new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = agentLegId,
                ReleaseWithCallControlId = targetLegId,
                Detached = true,
            }, cancellationToken);
        }

        await ReleaseAgentLegAsync(agentLegId, partyLegId, cancellationToken);

        return true;
    }

    /// <summary>
    /// Hands the party of a consult to the destination the agent consulted, and releases the agent's legs.
    /// </summary>
    /// <param name="consultLegId">The agent's consult leg.</param>
    /// <param name="consult">Its state.</param>
    /// <param name="hangUpConsultLeg">Whether the consult leg is still up and has to be released too.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether the party was handed over.</returns>
    public async Task<bool> CompleteConsultAsync(
        string consultLegId,
        TelnyxOutboundBridgeState consult,
        bool hangUpConsultLeg,
        CancellationToken cancellationToken)
    {
        var targetLegId = consult.PeerCallControlId;
        var targetIsBrowser = !string.IsNullOrWhiteSpace(consult.TargetUserId);

        // A colleague is talking to the agent in the consult's conference; they leave it (and are parked) before they
        // are bridged to the party.
        if (targetIsBrowser)
        {
            var conference = await _apiClient.FindConferenceByNameAsync(ConsultConferenceName(consultLegId), cancellationToken);

            if (conference.Succeeded && !string.IsNullOrWhiteSpace(conference.ConferenceId))
            {
                await _apiClient.LeaveConferenceAsync(conference.ConferenceId, targetLegId, cancellationToken);
            }
        }

        if (!await HandOverAsync(targetLegId, targetIsBrowser, consult.PartyCallControlId, consult.ConsultOfCallControlId, cancellationToken))
        {
            return false;
        }

        if (hangUpConsultLeg)
        {
            await HangupAsync(consultLegId, consult.AsDetached(), cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Rewrites the client state a leg carries on every later event.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="state">Its new state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task UpdateStateAsync(string callControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        var updated = await _apiClient.UpdateClientStateAsync(callControlId, state.ToClientStateJson(), cancellationToken);

        if (!updated.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(updated))
        {
            _logger.LogWarning(
                "Telnyx refused to update the state of leg {CallControlId} during a transfer ({StatusCode}).",
                callControlId.SanitizeLogValue(),
                updated.StatusCode);
        }
    }

    /// <summary>
    /// Hangs a leg up with the state its hang-up is reported with. A leg that is already gone has nothing left to do.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="state">The state its hang-up carries.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task HangupAsync(string callControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return;
        }

        var hangup = state is null
            ? await _apiClient.HangupAsync(callControlId, cancellationToken)
            : await _apiClient.HangupWithStateAsync(callControlId, state.ToClientStateJson(), cancellationToken);

        if (!hangup.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(hangup) && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx returned {StatusCode} hanging up leg {CallControlId} during a transfer (it may already be gone).",
                hangup.StatusCode,
                callControlId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Reads a leg, answering whether it is still up and what state it carries.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<(bool IsAlive, TelnyxOutboundBridgeState State)> ReadAsync(string callControlId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return (false, null);
        }

        var status = await _apiClient.GetCallStatusAsync(callControlId, cancellationToken);

        if (!status.Succeeded)
        {
            return (false, null);
        }

        return TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state)
            ? (status.IsAlive, state)
            : (status.IsAlive, null);
    }

    // The agent's leg is parked now that the party left it. Its hang-up must not reach back for the party, so it is
    // reported detached.
    private Task ReleaseAgentLegAsync(string agentLegId, string partyLegId, CancellationToken cancellationToken)
        => HangupAsync(agentLegId, new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            PeerCallControlId = partyLegId,
            Detached = true,
        }, cancellationToken);

    // The colleague's phone only acts on calls in their own history, and their history is what the leg's events settle.
    // It rings them, so it is recorded as an incoming call they have not answered yet.
    private async Task RecordColleagueCallAsync(string callControlId, TelnyxOutboundBridgeState state, CancellationToken cancellationToken)
    {
        if (_interactionStore is null)
        {
            return;
        }

        await _interactionStore.CreateAsync(new TelephonyInteraction
        {
            InteractionId = IdGenerator.GenerateId(),
            CallId = callControlId,
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            UserId = state.TargetUserId,
            From = state.PartyNumber,
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _clock?.UtcNow ?? DateTime.UtcNow,
            AwaitingAnswer = true,
        }, cancellationToken);
    }
}
