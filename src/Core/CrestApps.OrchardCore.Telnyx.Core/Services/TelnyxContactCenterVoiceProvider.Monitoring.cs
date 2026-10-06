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
/// <c>barge</c> by both). Stopping only hangs that leg up, and a takeover bridges the customer to a fresh ordinary leg
/// before the agent's leg is released. Nobody is moved. Live, moving the call into a conference for the supervisor left
/// the customer and the agent unable to hear each other, or the supervisor, in every mode.
/// </para>
/// <para>
/// A leg keeps the role it is dialed with. Live (2026-09-26), a leg dialed to listen and left alone was heard, but every
/// <c>switch_supervisor_role</c> Telnyx accepted went wrong: a leg switched from listening to whisper was heard by nobody,
/// one switched to barge left the supervisor hearing silence, and one dialed as barge and switched to listen when it
/// answered carried only silence. So a change of mode rings the supervisor's phone with a fresh leg dialed in the new role,
/// carrying the engagement's token (the phone answers it in place of the one it holds) and naming the leg it replaces,
/// which is let go once the new one answers (see TelnyxOutboundBridgeOrchestrator).
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

    // How long a takeover waits for the supervisor's phone to answer the leg it takes the call on, within the server's
    // command timeout, and how often it tries the bridge meanwhile.
    private static readonly TimeSpan _takeOverAnswerWait = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan _takeOverBridgeRetry = TimeSpan.FromMilliseconds(300);

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
            // Telnyx attaches the leg to the agent's when it answers, in the role it is dialed with; it is never switched.
            AttachToAgentLeg(originate, agentLegId, role);
        }

        // A browser credential is reached as an internal SIP address, never through the outbound voice profile. The
        // header is what the phone matches the leg to the engagement it asked for.
        AddMonitorToken(originate, token);
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

        if (!SupervisesInPlace(request))
        {
            return await SupervisedConference.SwitchRoleAsync(SupervisedConferenceName(request), supervisorLegId, role, request.AgentLegId?.Trim(), cancellationToken)
                ? MonitoringSuccess(request)
                : Failure("monitor_switch_failed", "The supervisor's mode could not be changed.");
        }

        if (string.IsNullOrWhiteSpace(request.AgentLegId))
        {
            return Failure("monitor_agent_leg_missing", "The agent's leg of this call is not known, so the mode cannot be changed.");
        }

        var replacementLegId = await RingReplacementLegAsync(request, supervisorLegId, role, cancellationToken);

        if (string.IsNullOrEmpty(replacementLegId))
        {
            return Failure("monitor_switch_failed", "Your soft phone could not be rung in the new mode.");
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = request.ProviderCallId.Trim(),

            // The engagement is on the new leg from now on; the one it replaces goes once the phone has answered it.
            ProviderLegId = replacementLegId,
        };
    }

    // Rings the supervisor's phone with a supervising leg dialed in the new role. It carries the engagement's token, so
    // the phone answers it in place of the leg it holds, and names that leg, which is hung up once this one answers.
    private async Task<string> RingReplacementLegAsync(
        ContactCenterVoiceMonitoringRequest request,
        string supervisorLegId,
        string role,
        CancellationToken cancellationToken)
    {
        var customerLegId = request.ProviderCallId.Trim();
        var agentLegId = request.AgentLegId.Trim();
        var token = await ResolveMonitorTokenAsync(request, supervisorLegId, cancellationToken);
        var endpoint = string.IsNullOrWhiteSpace(request.SupervisorId)
            ? null
            : await _agentEndpointResolver.ResolveAsync(request.SupervisorId.Trim(), cancellationToken);

        if (string.IsNullOrEmpty(token) || string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning(
                "The supervisor's mode on call '{CustomerLegId}' could not be changed: the engagement's token or the phone's address is unknown.",
                customerLegId.SanitizeLogValue());

            return null;
        }

        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = endpoint,
            From = _options.DefaultOutboundCallerId,
            TimeoutSeconds = SupervisorLegTimeoutSeconds,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
                PeerCallControlId = customerLegId,
                PartyCallControlId = agentLegId,
                SupervisesInPlace = true,
                SupervisorRole = role,
                RingUserId = request.SupervisorId.Trim(),
                MonitorToken = token,
                ReplacesCallControlId = supervisorLegId,
            }.ToClientStateJson(),
        };

        AttachToAgentLeg(originate, agentLegId, role);
        AddMonitorToken(originate, token);
        originate.AdditionalFields["command_id"] = $"cc-sv-mode-{token}-{Guid.NewGuid():N}";

        var leg = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!leg.Succeeded || string.IsNullOrWhiteSpace(leg.CallControlId))
        {
            _logger.LogError(
                "Telnyx rejected the {Role} supervisor leg replacing '{SupervisorLegId}' on call '{CustomerLegId}' with status code {StatusCode}; the supervisor stays in their mode. Response: {Response}",
                role.SanitizeLogValue(),
                supervisorLegId.SanitizeLogValue(),
                customerLegId.SanitizeLogValue(),
                leg.StatusCode,
                leg.ErrorBody.SanitizeLogValue());

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Rang supervisor leg '{ReplacementLegId}' as {Role} on agent leg '{AgentLegId}' to replace '{SupervisorLegId}'.",
                leg.CallControlId.SanitizeLogValue(),
                role.SanitizeLogValue(),
                agentLegId.SanitizeLogValue(),
                supervisorLegId.SanitizeLogValue());
        }

        return leg.CallControlId;
    }

    // Telnyx attaches the leg to the agent's when it answers, in the role it is dialed with: a whisper is heard by the
    // agent alone.
    private static void AttachToAgentLeg(TelnyxOriginateRequest originate, string agentLegId, string role)
    {
        originate.AdditionalFields["supervise_call_control_id"] = agentLegId;
        originate.AdditionalFields["supervisor_role"] = role;
    }

    // The header the phone matches a leg to its engagement by, for an SDK that hands over no client state.
    private static void AddMonitorToken(TelnyxOriginateRequest originate, string token)
        => originate.AdditionalFields["custom_headers"] = new[]
        {
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = TelnyxConstants.MonitorLegSipHeader,
                ["value"] = token,
            },
        };

    // The engagement's token, read from the request or else from the supervising leg the phone answered it on (a
    // Contact Center call's request carries none).
    private async Task<string> ResolveMonitorTokenAsync(
        ContactCenterVoiceMonitoringRequest request,
        string supervisorLegId,
        CancellationToken cancellationToken)
    {
        var token = request.MonitorToken?.Trim();

        if (!string.IsNullOrEmpty(token))
        {
            return token;
        }

        var supervising = await _apiClient.GetCallStatusAsync(supervisorLegId, cancellationToken);

        return supervising.Succeeded && TelnyxOutboundBridgeState.TryParseEncoded(supervising.ClientState, out var supervisingState)
            ? supervisingState.MonitorToken
            : null;
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

        if (SupervisesInPlace(request))
        {
            // The customer is bridged to the supervisor before the agent goes, so they are never alone on the line. The
            // supervising leg is not switched to barge first: live, that left the supervisor hearing silence.
            //
            // Telnyx takes no command on a supervising leg ("Supervisor calls do not support commands"), so the customer
            // cannot be bridged to it. The supervisor's phone is rung with an ordinary leg that carries the engagement's
            // token, which the phone answers by itself in place of the one it holds, and the customer is bridged to that.
            var takeOverLegId = await RingTakeOverLegAsync(request, customerLegId, agentLegId, supervisorLegId, cancellationToken);

            if (string.IsNullOrEmpty(takeOverLegId))
            {
                return Failure("takeover_failed", "Your soft phone could not be rung to take the call.");
            }

            if (!await BridgeWhenAnsweredAsync(takeOverLegId, customerLegId, cancellationToken))
            {
                await _apiClient.HangupWithStateAsync(takeOverLegId, DetachedSupervisorState(request).ToClientStateJson(), CancellationToken.None);

                return Failure("takeover_failed", "The call could not be handed to you.");
            }

            // The supervising leg hears nothing any more; it goes quietly, since the engagement lives on the new leg.
            await _apiClient.HangupWithStateAsync(supervisorLegId, DetachedSupervisorState(request).ToClientStateJson(), CancellationToken.None);
            supervisorLegId = takeOverLegId;

            await HandCustomerToAsync(customerLegId, agentLegId, takeOverLegId, cancellationToken);
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

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = customerLegId,

            // The leg the call is on now, which is the one to record as the supervisor's.
            ProviderLegId = supervisorLegId,
        };
    }

    // The customer's leg names the agent's as the one it hangs up when it ends. The agent is gone: it names the leg the
    // call was taken over on instead, so the customer hanging up ends the supervisor's side rather than leaving it parked.
    private async Task HandCustomerToAsync(string customerLegId, string agentLegId, string takeOverLegId, CancellationToken cancellationToken)
    {
        var customer = await _apiClient.GetCallStatusAsync(customerLegId, cancellationToken);

        if (!customer.Succeeded ||
            !TelnyxOutboundBridgeState.TryParseEncoded(customer.ClientState, out var customerState) ||
            !string.Equals(customerState.PeerCallControlId, agentLegId, StringComparison.Ordinal))
        {
            return;
        }

        var updated = await _apiClient.UpdateClientStateAsync(customerLegId, customerState.WithPeer(takeOverLegId).ToClientStateJson(), cancellationToken);

        if (!updated.Succeeded)
        {
            _logger.LogWarning(
                "Telnyx refused to hand leg '{CustomerLegId}' to the supervisor who took the call over ({StatusCode}); when it hangs up, the supervisor's leg is left parked.",
                customerLegId.SanitizeLogValue(),
                updated.StatusCode);
        }
    }

    // Rings the supervisor's phone with the leg they take the call over on. The token is the engagement's, read from the
    // request or else from the supervising leg the phone answered it on.
    private async Task<string> RingTakeOverLegAsync(
        ContactCenterVoiceMonitoringRequest request,
        string customerLegId,
        string agentLegId,
        string supervisorLegId,
        CancellationToken cancellationToken)
    {
        var token = await ResolveMonitorTokenAsync(request, supervisorLegId, cancellationToken);
        var endpoint = string.IsNullOrWhiteSpace(request.SupervisorId)
            ? null
            : await _agentEndpointResolver.ResolveAsync(request.SupervisorId.Trim(), cancellationToken);

        if (string.IsNullOrEmpty(token) || string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning(
                "A takeover of call '{CustomerLegId}' could not ring the supervisor's phone: the engagement's token or the phone's address is unknown.",
                customerLegId.SanitizeLogValue());

            return null;
        }

        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = endpoint,
            From = _options.DefaultOutboundCallerId,
            TimeoutSeconds = SupervisorLegTimeoutSeconds,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterSupervisorLegIntent,
                PeerCallControlId = customerLegId,
                PartyCallControlId = agentLegId,
                SupervisesInPlace = true,
                TakesOver = true,
                SupervisorRole = "barge",
                RingUserId = request.SupervisorId.Trim(),
                MonitorToken = token,
            }.ToClientStateJson(),
        };

        AddMonitorToken(originate, token);
        originate.AdditionalFields["command_id"] = $"cc-sv-take-{token}";

        var leg = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!leg.Succeeded || string.IsNullOrWhiteSpace(leg.CallControlId))
        {
            _logger.LogError(
                "Telnyx rejected the takeover leg for call '{CustomerLegId}' with status code {StatusCode}. Response: {Response}",
                customerLegId.SanitizeLogValue(),
                leg.StatusCode,
                leg.ErrorBody.SanitizeLogValue());

            return null;
        }

        return leg.CallControlId;
    }

    // Bridges the customer to the takeover leg as soon as the supervisor's phone has answered it. A leg still ringing is
    // refused as not answered yet, so the bridge is tried again until it holds, is refused for another reason, or the
    // wait is over.
    private async Task<bool> BridgeWhenAnsweredAsync(string takeOverLegId, string customerLegId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _takeOverAnswerWait;
        TelnyxApiResult bridged;

        while (true)
        {
            // The customer leaves the agent's leg, which its own bridge's park_after_unbridge=self parks rather than
            // hangs up; the takeover leg is parked, not hung up, if it is unbridged later -- as every agent leg is.
            bridged = await _apiClient.PostCallActionAsync(takeOverLegId, "bridge", new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["call_control_id"] = customerLegId,
                ["park_after_unbridge"] = "self",
            }, cancellationToken);

            if (bridged.Succeeded || !TelnyxApiErrors.IsCallNotAnsweredYet(bridged) || DateTime.UtcNow + _takeOverBridgeRetry > deadline)
            {
                break;
            }

            await Task.Delay(_takeOverBridgeRetry, cancellationToken);
        }

        if (!bridged.Succeeded)
        {
            _logger.LogError(
                "Telnyx refused to bridge call '{CustomerLegId}' to takeover leg '{TakeOverLegId}' with status code {StatusCode}; the agent stays on the call. Response: {Response}",
                customerLegId.SanitizeLogValue(),
                takeOverLegId.SanitizeLogValue(),
                bridged.StatusCode,
                bridged.ErrorBody.SanitizeLogValue());

            return false;
        }

        // The supervisor is now the one the customer talks to.
        await ApplyNoiseSuppressionAsync(takeOverLegId, TelnyxNoiseSuppressionLeg.Agent, cancellationToken);

        return true;
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
