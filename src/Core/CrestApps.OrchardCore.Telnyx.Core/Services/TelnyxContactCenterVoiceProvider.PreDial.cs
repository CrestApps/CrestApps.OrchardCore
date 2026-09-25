using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Rings the agent's browser while their offer is still ringing, and joins or hangs up that leg when told to.
/// </summary>
public sealed partial class TelnyxContactCenterVoiceProvider
{
    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> PreDialAgentAsync(
        ContactCenterAgentPreDialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterVoiceWorkPartition);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Telnyx Contact Center voice provider is temporarily unavailable.");
        }

        if (!_options.IsConfigured)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.ReservationId) ||
            string.IsNullOrWhiteSpace(request.ProviderCallId) ||
            string.IsNullOrWhiteSpace(request.AgentUserId))
        {
            return Failure("pre_dial_invalid", "An offer, a caller call id and an agent are required to pre-dial the agent.");
        }

        // Only a browser that said it can hold an offer's leg is rung early. One that cannot would ring it as a second
        // incoming call on top of the offer, or answer it on arrival and put the agent on a silent line.
        var agentEndpoint = await _agentEndpointResolver.ResolveAsync(
            request.AgentUserId,
            TelephonyConstants.SoftPhoneClientCapabilities.HeldOfferLeg,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(agentEndpoint))
        {
            return Failure("agent_endpoint_unsupported", "The agent has no live Telnyx soft-phone registration that can hold an offer's leg.");
        }

        var reservationId = request.ReservationId.Trim();
        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = agentEndpoint,
            From = _options.DefaultOutboundCallerId,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent,
                PeerCallControlId = request.ProviderCallId.Trim(),
                ReservationId = reservationId,
            }.ToClientStateJson(),

            // The leg rings for as long as its offer does, so one nobody answers ends with the offer on its own.
            // Telnyx accepts a ring window of five seconds to ten minutes.
            TimeoutSeconds = request.TimeoutSeconds > 0 ? Math.Clamp(request.TimeoutSeconds, 5, 600) : null,
        };

        // The offer id also rides in a SIP header, which the browser SDK exposes on the incoming call alongside the
        // client state. Either is enough for the browser to tie the leg to the offer it is showing.
        originate.AdditionalFields["custom_headers"] = new[]
        {
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = TelnyxConstants.OfferIdSipHeader,
                ["value"] = reservationId,
            },
        };

        try
        {
            var result = await _apiClient.OriginateAsync(originate, cancellationToken);

            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.CallControlId))
            {
                _logger.LogWarning(
                    "Telnyx refused to pre-dial the agent for an offer with status code {StatusCode}; the accept will connect them. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());

                return Failure("pre_dial_failed", "The Telnyx agent leg could not be originated ahead of the accept.");
            }

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId.Trim(),
                ProviderLegId = result.CallControlId,
                ProviderLegState = VoiceCallState.Dialing,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while pre-dialing a Telnyx agent leg for an offer.");

            return Failure("pre_dial_failed", "The Telnyx agent leg could not be originated ahead of the accept.");
        }
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> BridgePreDialedAgentAsync(
        string providerCallId,
        string agentLegId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId) || string.IsNullOrWhiteSpace(agentLegId))
        {
            return Failure("bridge_invalid", "A caller call id and an agent leg are required to join them.");
        }

        var callerCallControlId = providerCallId.Trim();
        var agentCallControlId = agentLegId.Trim();

        try
        {
            // Telnyx de-duplicates by command_id, so a second trigger cannot bridge the leg twice.
            var result = await _apiClient.PostCallActionAsync(
                agentCallControlId,
                "bridge",
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["call_control_id"] = callerCallControlId,
                    ["command_id"] = $"cc-predial-bridge-{agentCallControlId}",

                    // A warm transfer moves the caller out of this bridge into a consult conference; parked, the
                    // agent's leg survives that and can follow them in. Every other ending releases it explicitly.
                    ["park_after_unbridge"] = "self",
                },
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Telnyx rejected joining a pre-dialed agent leg to the caller with status code {StatusCode}. Response: {Response}",
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());

                return Failure("bridge_failed", "The Telnyx pre-dialed agent leg could not be joined to the caller.");
            }

            // The caller heard the queue until this moment; now that the agent is on the line it stops.
            await StopCallerPlaybackAsync(callerCallControlId, cancellationToken);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderName = TechnicalName,
                ProviderCallId = callerCallControlId,
                ProviderLegId = agentCallControlId,
                ProviderLegState = VoiceCallState.Connected,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while joining a pre-dialed Telnyx agent leg to the caller.");

            return Failure("bridge_failed", "The Telnyx pre-dialed agent leg could not be joined to the caller.");
        }
    }

    /// <inheritdoc/>
    public async Task HangupPreDialedAgentAsync(string agentLegId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentLegId))
        {
            return;
        }

        // A leg that has already ended is refused here, which is the answer expected when the browser declined it
        // first or its ring window ran out; the result is deliberately not inspected.
        await _apiClient.HangupAsync(agentLegId.Trim(), cancellationToken);
    }

    private async Task<ContactCenterVoiceProviderResult> ConnectPreDialedAgentAsync(
        ContactCenterConnectRequest request,
        CancellationToken cancellationToken)
    {
        var callerCallControlId = request.ProviderCallId.Trim();

        try
        {
            // Telnyx refuses to bridge a leg that is not answered, so the caller is answered before the agent leg is
            // joined. A caller already answered (listening to the queue) simply fails here, which is ignored.
            var answerResult = await _apiClient.AnswerAsync(callerCallControlId, cancellationToken: cancellationToken);

            if (!answerResult.Succeeded && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telnyx returned {StatusCode} answering the caller leg before joining a pre-dialed agent leg (it may already be answered).",
                    answerResult.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while answering the caller leg before joining a pre-dialed agent leg.");
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = callerCallControlId,
            ProviderLegId = request.PreDialedAgentLegId.Trim(),

            // Rung, and possibly answered by the browser already; the Contact Center joins it and reports it
            // answered once it knows.
            ProviderLegState = VoiceCallState.Dialing,
        };
    }

    private async Task StopCallerPlaybackAsync(string callerCallControlId, CancellationToken cancellationToken)
    {
        try
        {
            // Idempotent: a caller with nothing playing is refused (422), which is the expected answer.
            var result = await _apiClient.StopPlaybackAsync(callerCallControlId, cancellationToken);

            if (!result.Succeeded && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telnyx returned {StatusCode} stopping the caller's hold music after joining the agent (nothing may have been playing).",
                    result.StatusCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while stopping the caller's hold music after joining the agent.");
        }
    }
}
