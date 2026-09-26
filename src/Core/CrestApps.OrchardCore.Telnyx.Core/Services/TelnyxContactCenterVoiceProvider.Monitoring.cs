using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Supervisor listen, whisper and barge on Telnyx, and the interventions built on them.
/// </summary>
/// <remarks>
/// <para>
/// The supervisor hears the call on their own soft phone. Engaging rings the supervisor's registered browser credential
/// (the one the resolver every agent dial uses picks: registered first) with a leg that carries the intent
/// <see cref="TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent"/> and the one-off token the phone was told to
/// expect, in its client state and in the <see cref="TelnyxConstants.MonitorLegSipHeader"/> header. The phone answers
/// that leg by itself.
/// </para>
/// <para>
/// A call on a two-leg bridge -- a Contact Center call, a number dialed from the keypad -- is supervised where it is: the
/// leg is dialed with <c>supervise_call_control_id</c> naming the agent's leg and a <c>supervisor_role</c>, and Telnyx
/// attaches it to the call when it answers (<c>monitor</c> heard by nobody, <c>whisper</c> by the agent alone,
/// <c>barge</c> by both). A mode is changed on the supervisor's own leg (<c>switch_supervisor_role</c>), stopping only hangs
/// that leg up, and a takeover bridges the customer to it before the agent's leg is released. Nobody is moved. Live,
/// moving the call into a conference for the supervisor left the customer and the agent unable to hear each other, or the
/// supervisor, in every mode.
/// </para>
/// <para>
/// An extension call already runs in a conference of its own, and the supervisor joins it there (see
/// TelnyxSupervisedConference).
/// </para>
/// </remarks>
public sealed partial class TelnyxContactCenterVoiceProvider :
    IContactCenterVoiceMonitoringProvider,
    IContactCenterVoiceSupervisorInterventionProvider
{
    // How long the supervisor's phone is given to answer its own monitor leg; it answers without ringing.
    private const int SupervisorLegTimeoutSeconds = 30;

    private TelnyxSupervisedConference _supervisedConference;

    private TelnyxSupervisedConference SupervisedConference
        => _supervisedConference ??= new TelnyxSupervisedConference(_apiClient, _logger);

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> EngageAsync(
        ContactCenterVoiceMonitoringRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ProviderCallId) || string.IsNullOrWhiteSpace(request.SupervisorId))
        {
            return Failure("monitor_invalid", "A call and a supervisor are required to start a supervisor engagement.");
        }

        if (string.IsNullOrWhiteSpace(request.AgentLegId))
        {
            return Failure("monitor_agent_leg_missing", "The agent's leg of this call is not known yet, so it cannot be supervised.");
        }

        var supervisorEndpoint = await _agentEndpointResolver.ResolveAsync(request.SupervisorId.Trim(), cancellationToken);

        if (string.IsNullOrWhiteSpace(supervisorEndpoint))
        {
            return Failure("supervisor_endpoint_missing", "Open your soft phone first: the call is played to you through it.");
        }

        var customerLegId = request.ProviderCallId.Trim();
        var agentLegId = request.AgentLegId.Trim();
        var token = string.IsNullOrWhiteSpace(request.MonitorToken) ? Guid.NewGuid().ToString("N") : request.MonitorToken.Trim();
        var role = TelnyxSupervisedConference.RoleFor(request.Mode.ToString());
        var inPlace = SupervisesInPlace(request);

        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = supervisorEndpoint,
            From = _options.DefaultOutboundCallerId,
            TimeoutSeconds = SupervisorLegTimeoutSeconds,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
                PeerCallControlId = customerLegId,
                PartyCallControlId = agentLegId,
                ConferenceName = inPlace ? null : SupervisedConferenceName(request),
                SupervisesInPlace = inPlace ? true : null,
                SupervisorRole = role,
                RingUserId = request.SupervisorId.Trim(),
                MonitorToken = token,
            }.ToClientStateJson(),
        };

        if (inPlace)
        {
            // Telnyx attaches the leg to the agent's when it answers: a whisper is heard by the agent alone.
            originate.AdditionalFields["supervise_call_control_id"] = agentLegId;
            originate.AdditionalFields["supervisor_role"] = role;
        }

        // A browser credential is reached as an internal SIP address, never through the outbound voice profile. The
        // header is what the phone matches the leg to the engagement it asked for.
        originate.AdditionalFields["custom_headers"] = new[]
        {
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = TelnyxConstants.MonitorLegSipHeader,
                ["value"] = token,
            },
        };
        originate.AdditionalFields["command_id"] = $"cc-sv-leg-{token}";

        var leg = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!leg.Succeeded || string.IsNullOrWhiteSpace(leg.CallControlId))
        {
            _logger.LogError(
                "Telnyx rejected the supervisor leg for call '{CallControlId}' with status code {StatusCode}. Response: {Response}",
                customerLegId.SanitizeLogValue(),
                leg.StatusCode,
                leg.ErrorBody.SanitizeLogValue());

            return Failure("monitor_failed", "Your soft phone could not be rung to listen to the call.");
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = customerLegId,
            ProviderLegId = leg.CallControlId,

            // The phone answers it by itself within a second; until then it is ringing there.
            ProviderLegState = VoiceCallState.Dialing,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["monitorToken"] = token,
            },
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> StopAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SupervisorLegId))
        {
            // Nothing of the supervisor's is up: an engagement whose leg never existed is already stopped.
            return MonitoringSuccess(request);
        }

        var supervisorLegId = request.SupervisorLegId.Trim();

        // Marked detached, so its hang-up is not read as the supervisor walking away from a call that is still theirs.
        var hangup = await _apiClient.HangupWithStateAsync(supervisorLegId, DetachedSupervisorState(request).ToClientStateJson(), cancellationToken);

        if (!hangup.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(hangup) && hangup.StatusCode is not null)
        {
            _logger.LogWarning(
                "Telnyx returned {StatusCode} hanging up supervisor leg '{SupervisorLegId}'. Response: {Response}",
                hangup.StatusCode,
                supervisorLegId.SanitizeLogValue(),
                hangup.ErrorBody.SanitizeLogValue());
        }

        if (hangup.StatusCode is null)
        {
            return Failure("monitor_stop_outcome_unknown", "Telnyx could not be reached to stop the supervisor engagement.");
        }

        if (SupervisesInPlace(request))
        {
            // Nothing was moved for the supervisor: the call is as it was.
            return MonitoringSuccess(request);
        }

        await SupervisedConference.RestoreIfUnsupervisedAsync(
            request.ProviderCallId?.Trim(),
            request.AgentLegId?.Trim(),
            supervisorLegId,
            cancellationToken);

        return MonitoringSuccess(request);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> SwitchModeAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderCallId) || string.IsNullOrWhiteSpace(request.SupervisorLegId))
        {
            return Failure("monitor_invalid", "The supervisor engagement could not be found.");
        }

        var supervisorLegId = request.SupervisorLegId.Trim();
        var role = TelnyxSupervisedConference.RoleFor(request.Mode.ToString());

        if (SupervisesInPlace(request)
            ? (await _apiClient.SwitchSupervisorRoleAsync(supervisorLegId, role, cancellationToken)).Succeeded
            : await SupervisedConference.SwitchRoleAsync(SupervisedConferenceName(request), supervisorLegId, role, request.AgentLegId?.Trim(), cancellationToken))
        {
            return MonitoringSuccess(request);
        }

        // The phone has not answered yet, so the leg is not in the conference: the role it joins with is changed instead.
        var status = await _apiClient.GetCallStatusAsync(supervisorLegId, cancellationToken);

        if (status.Succeeded &&
            status.IsAlive &&
            TelnyxOutboundBridgeState.TryParseEncoded(status.ClientState, out var state) &&
            state.Intent == TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent)
        {
            state.SupervisorRole = role;

            var updated = await _apiClient.UpdateClientStateAsync(supervisorLegId, state.ToClientStateJson(), cancellationToken);

            if (updated.Succeeded)
            {
                return MonitoringSuccess(request);
            }
        }

        return Failure("monitor_switch_failed", "The supervisor's mode could not be changed.");
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> TakeOverAsync(
        ContactCenterVoiceMonitoringRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderCallId) ||
            string.IsNullOrWhiteSpace(request.SupervisorLegId) ||
            string.IsNullOrWhiteSpace(request.AgentLegId))
        {
            return Failure("takeover_invalid", "A takeover needs the call, the supervisor's leg and the agent's leg.");
        }

        var customerLegId = request.ProviderCallId.Trim();
        var agentLegId = request.AgentLegId.Trim();
        var supervisorLegId = request.SupervisorLegId.Trim();

        // The supervisor is heard by the customer before the agent goes, so the customer is never alone on the line.
        if (SupervisesInPlace(request))
        {
            var heard = await _apiClient.SwitchSupervisorRoleAsync(supervisorLegId, "barge", cancellationToken);

            if (!heard.Succeeded)
            {
                return Failure("takeover_failed", "You are not connected to the call yet, so it cannot be taken over.");
            }

            // The customer is bridged to the supervisor's own leg, which takes them off the agent's (parked by its own
            // bridge's park_after_unbridge=self) and leaves the supervisor's leg parked, not hung up, if it is unbridged
            // later -- as every agent leg is.
            var bridged = await _apiClient.PostCallActionAsync(supervisorLegId, "bridge", new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["call_control_id"] = customerLegId,
                ["park_after_unbridge"] = "self",
            }, cancellationToken);

            if (!bridged.Succeeded)
            {
                _logger.LogError(
                    "Telnyx refused to bridge call '{CustomerLegId}' to supervisor leg '{SupervisorLegId}' for a takeover with status code {StatusCode}; the agent stays on the call. Response: {Response}",
                    customerLegId.SanitizeLogValue(),
                    supervisorLegId.SanitizeLogValue(),
                    bridged.StatusCode,
                    bridged.ErrorBody.SanitizeLogValue());

                return Failure("takeover_failed", "The call could not be handed to you.");
            }
        }
        else if (!await SupervisedConference.SwitchRoleAsync(SupervisedConferenceName(request), supervisorLegId, "barge", agentLegId, cancellationToken))
        {
            return Failure("takeover_failed", "You are not connected to the call yet, so it cannot be taken over.");
        }

        // The agent's leg is released marked detached: its hang-up comes back as a webhook that would otherwise end the
        // call for the customer.
        var agentStatus = await _apiClient.GetCallStatusAsync(agentLegId, cancellationToken);
        var agentState = agentStatus.Succeeded && TelnyxOutboundBridgeState.TryParseEncoded(agentStatus.ClientState, out var parsed)
            ? parsed.AsDetached()
            : new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
                PeerCallControlId = customerLegId,
                Detached = true,
            };

        var hangup = await _apiClient.HangupWithStateAsync(agentLegId, agentState.ToClientStateJson(), cancellationToken);

        if (!hangup.Succeeded && !TelnyxApiErrors.IsCallAlreadyEnded(hangup))
        {
            _logger.LogError(
                "Telnyx refused to release agent leg '{AgentLegId}' for a takeover with status code {StatusCode}. Response: {Response}",
                agentLegId.SanitizeLogValue(),
                hangup.StatusCode,
                hangup.ErrorBody.SanitizeLogValue());

            return Failure("takeover_failed", "The agent could not be released from the call.");
        }

        return MonitoringSuccess(request);
    }

    /// <inheritdoc/>
    public async Task ReleaseSupervisorLegAsync(string supervisorLegId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorLegId))
        {
            return;
        }

        var result = await _apiClient.HangupWithStateAsync(
            supervisorLegId.Trim(),
            new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
                Detached = true,
            }.ToClientStateJson(),
            cancellationToken);

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx returned {StatusCode} releasing supervisor leg '{SupervisorLegId}' after its call ended (it may already be gone).",
                result.StatusCode,
                supervisorLegId.SanitizeLogValue());
        }
    }

    // A call on a two-leg bridge is supervised where it is; one that runs in a conference of its own -- an extension call,
    // which the request names -- is joined there.
    private static bool SupervisesInPlace(ContactCenterVoiceMonitoringRequest request)
        => TelnyxSupervisedConference.IsOwnConference(SupervisedConferenceName(request), request.ProviderCallId?.Trim());

    private static TelnyxOutboundBridgeState DetachedSupervisorState(ContactCenterVoiceMonitoringRequest request)
        => new()
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
            PeerCallControlId = request.ProviderCallId?.Trim(),
            PartyCallControlId = request.AgentLegId?.Trim(),
            RingUserId = request.SupervisorId?.Trim(),
            Detached = true,
        };

    private ContactCenterVoiceProviderResult MonitoringSuccess(ContactCenterVoiceMonitoringRequest request)
        => new()
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = request.ProviderCallId?.Trim(),
            ProviderLegId = request.SupervisorLegId?.Trim(),
        };
}
