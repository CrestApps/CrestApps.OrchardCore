using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Warm (attended) transfer on Telnyx, built on a conference.
/// </summary>
/// <remarks>
/// A two-leg bridge cannot hold one party while the other two talk, so the consult moves the call into a
/// conference named for the consult: the customer is held there with the queue's hold music, the agent's leg joins
/// them, and the destination is rung on a leg of its own that joins when it answers. Completing unholds the customer
/// with the destination; cancelling drops the destination and unholds the customer with the agent. The agent's leg
/// is never hung up here: the Contact Center hangs it up only after it has recorded that the leg no longer carries
/// the call, because that hangup comes back as a webhook that would otherwise end the call.
/// </remarks>
public sealed partial class TelnyxContactCenterVoiceProvider : IContactCenterVoiceAttendedTransferProvider
{
    // How long the destination rings before the consult is given up and the customer returned to the agent.
    private const int ConsultRingTimeoutSeconds = 30;

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> BeginConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
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

        var customerLegId = request.ProviderCallId?.Trim();
        var agentLegId = Read(request, ContactCenterConstants.AttendedTransferMetadata.AgentLegId);
        var consultId = Read(request, "consultId");

        if (string.IsNullOrEmpty(customerLegId) || string.IsNullOrEmpty(agentLegId) || string.IsNullOrEmpty(consultId))
        {
            return Failure("consult_invalid", "A consult needs the customer's call, the agent's leg and a consult id.");
        }

        var (destination, isInternal) = await ResolveConsultDestinationAsync(request, cancellationToken);

        if (string.IsNullOrEmpty(destination))
        {
            return Failure("consult_destination_missing", "The consult destination cannot be reached: the agent has no live soft-phone registration, or no number was given.");
        }

        var conferenceName = ConsultConferenceName(consultId);

        // The customer moves first. The agent's leg was bridged with park-after-unbridge, so taking the customer out
        // of the bridge parks the agent's leg rather than hanging it up.
        var conference = await _apiClient.CreateConferenceAsync(conferenceName, customerLegId, commandId: $"cc-consult-conf-{consultId}", cancellationToken);

        if (!conference.Succeeded || string.IsNullOrWhiteSpace(conference.ConferenceId))
        {
            _logger.LogError(
                "Telnyx rejected the consult conference for call '{CallControlId}' with status code {StatusCode}. Response: {Response}",
                customerLegId.SanitizeLogValue(),
                conference.StatusCode,
                conference.ErrorBody.SanitizeLogValue());

            return Failure("consult_failed", "The customer could not be held for the consult.");
        }

        var hold = await _apiClient.HoldConferenceParticipantAsync(
            conference.ConferenceId,
            customerLegId,
            Read(request, ContactCenterConstants.AttendedTransferMetadata.HoldAudio),
            cancellationToken);

        if (!hold.Succeeded)
        {
            // The customer is still in the conference with the agent; they hear the consult instead of music, which
            // is worse than it should be but not a reason to drop anybody.
            _logger.LogWarning(
                "Telnyx returned {StatusCode} holding the customer for a consult. Response: {Response}",
                hold.StatusCode,
                hold.ErrorBody.SanitizeLogValue());
        }

        var join = await _apiClient.JoinConferenceAsync(conference.ConferenceId, agentLegId, endConferenceOnExit: false, commandId: $"cc-consult-agent-{consultId}", cancellationToken);

        if (!join.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected joining the agent leg to the consult conference with status code {StatusCode}. Response: {Response}",
                join.StatusCode,
                join.ErrorBody.SanitizeLogValue());

            await _apiClient.UnholdConferenceParticipantAsync(conference.ConferenceId, customerLegId, cancellationToken);

            return Failure("consult_failed", "The agent could not be moved to the consult.");
        }

        var originate = new TelnyxOriginateRequest
        {
            ConnectionId = _options.ConnectionId,
            To = destination,
            From = _options.DefaultOutboundCallerId,
            TimeoutSeconds = ConsultRingTimeoutSeconds,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterConsultLegIntent,
                PeerCallControlId = customerLegId,
                ConferenceName = conferenceName,
                ConsultId = consultId,
            }.ToClientStateJson(),
        };

        originate.AdditionalFields["command_id"] = $"cc-consult-leg-{consultId}";

        // An outside number is terminated through the outbound voice profile; a colleague's browser must not be, or
        // Telnyx routes the leg to the PSTN and it never reaches the registered credential.
        if (!isInternal && !string.IsNullOrWhiteSpace(_options.OutboundVoiceProfileId))
        {
            originate.AdditionalFields["outbound_voice_profile_id"] = _options.OutboundVoiceProfileId;
        }

        var consultLeg = await _apiClient.OriginateAsync(originate, cancellationToken);

        if (!consultLeg.Succeeded || string.IsNullOrWhiteSpace(consultLeg.CallControlId))
        {
            _logger.LogError(
                "Telnyx rejected the consult leg with status code {StatusCode}. Response: {Response}",
                consultLeg.StatusCode,
                consultLeg.ErrorBody.SanitizeLogValue());

            // Nobody is being rung, so the customer comes straight back to the agent they were talking to.
            await _apiClient.UnholdConferenceParticipantAsync(conference.ConferenceId, customerLegId, cancellationToken);

            return Failure("consult_failed", "The consult destination could not be called.");
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = customerLegId,
            ProviderLegId = consultLeg.CallControlId,
            ProviderLegState = Telephony.Models.VoiceCallState.Dialing,
        };
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CompleteConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerLegId = request.ProviderCallId?.Trim();
        var consultLegId = Read(request, "consultLegId");
        var conferenceId = await FindConsultConferenceAsync(request, cancellationToken);

        if (string.IsNullOrEmpty(customerLegId) || string.IsNullOrEmpty(consultLegId) || string.IsNullOrEmpty(conferenceId))
        {
            return Failure("consult_invalid", "The consult could not be found.");
        }

        var unhold = await _apiClient.UnholdConferenceParticipantAsync(conferenceId, customerLegId, cancellationToken);

        if (!unhold.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected taking the customer off hold to complete a consult with status code {StatusCode}. Response: {Response}",
                unhold.StatusCode,
                unhold.ErrorBody.SanitizeLogValue());

            return Failure("consult_complete_failed", "The customer could not be joined to the destination.");
        }

        if (!string.Equals(
            Read(request, ContactCenterConstants.AttendedTransferMetadata.TargetType),
            nameof(InteractionTransferTargetType.External),
            StringComparison.OrdinalIgnoreCase))
        {
            // A colleague's browser stays in the conference with the customer: two browser legs, or a browser leg
            // and a phone, pass audio both ways through the mixer, and the Contact Center releases the customer
            // when either of them hangs up.
            return ConsultSuccess(customerLegId, consultLegId);
        }

        // An outside party takes the call out of the contact center, which then stops following it. The two phone
        // legs are joined directly instead, with the bridge issued on the destination's leg so that Telnyx hangs it
        // up when the customer goes; the destination going first is reported through its leg's own hangup.
        await _apiClient.LeaveConferenceAsync(conferenceId, customerLegId, cancellationToken);
        await _apiClient.LeaveConferenceAsync(conferenceId, consultLegId, cancellationToken);

        var bridge = await _apiClient.PostCallActionAsync(consultLegId, "bridge", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["call_control_id"] = customerLegId,
            ["command_id"] = $"cc-consult-bridge-{Read(request, "consultId")}",
        }, cancellationToken);

        if (!bridge.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected bridging the customer to the external destination of a consult with status code {StatusCode}. Response: {Response}",
                bridge.StatusCode,
                bridge.ErrorBody.SanitizeLogValue());

            return Failure("consult_complete_failed", "The customer could not be joined to the external destination.");
        }

        return ConsultSuccess(customerLegId, consultLegId);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> CancelConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customerLegId = request.ProviderCallId?.Trim();
        var consultLegId = Read(request, "consultLegId");
        var endedBy = Read(request, ContactCenterConstants.AttendedTransferMetadata.EndedBy) ?? ContactCenterConstants.AttendedTransferMetadata.EndedByAgent;

        // Whoever is still on the line is dealt with; whoever already left is not asked to leave again.
        if (!string.Equals(endedBy, ContactCenterConstants.AttendedTransferMetadata.EndedByTarget, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(consultLegId))
        {
            var hangup = await _apiClient.HangupAsync(consultLegId, cancellationToken);

            if (!hangup.Succeeded && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Telnyx returned {StatusCode} hanging up a consult leg (it may already be gone).", hangup.StatusCode);
            }
        }

        if (string.Equals(endedBy, ContactCenterConstants.AttendedTransferMetadata.EndedByCaller, StringComparison.Ordinal))
        {
            return ConsultSuccess(customerLegId, consultLegId);
        }

        var conferenceId = await FindConsultConferenceAsync(request, cancellationToken);

        if (string.IsNullOrEmpty(conferenceId) || string.IsNullOrEmpty(customerLegId))
        {
            return Failure("consult_invalid", "The consult could not be found.");
        }

        var unhold = await _apiClient.UnholdConferenceParticipantAsync(conferenceId, customerLegId, cancellationToken);

        if (!unhold.Succeeded)
        {
            _logger.LogError(
                "Telnyx rejected taking the customer off hold after a consult ended with status code {StatusCode}. Response: {Response}",
                unhold.StatusCode,
                unhold.ErrorBody.SanitizeLogValue());

            return Failure("consult_cancel_failed", "The customer could not be taken off hold.");
        }

        return ConsultSuccess(customerLegId, consultLegId);
    }

    /// <summary>
    /// The conference a consult holds its customer in, named for the consult so every phase finds the same one.
    /// </summary>
    /// <param name="consultId">The consult.</param>
    /// <returns>The conference name.</returns>
    public static string ConsultConferenceName(string consultId)
        => $"cc-consult-{consultId}";

    private async Task<(string Destination, bool IsInternal)> ResolveConsultDestinationAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken)
    {
        var agentUserId = Read(request, ContactCenterConstants.AttendedTransferMetadata.AgentUserId);

        if (!string.IsNullOrEmpty(agentUserId))
        {
            // The same resolver every agent dial uses: the credential the browser is actually registered on.
            return (await _agentEndpointResolver.ResolveAsync(agentUserId, cancellationToken), true);
        }

        return (Read(request, "targetAddress"), false);
    }

    private async Task<string> FindConsultConferenceAsync(ContactCenterVoiceAttendedTransferRequest request, CancellationToken cancellationToken)
    {
        var consultId = Read(request, "consultId");

        if (string.IsNullOrEmpty(consultId))
        {
            return null;
        }

        var conference = await _apiClient.FindConferenceByNameAsync(ConsultConferenceName(consultId), cancellationToken);

        return conference.Succeeded ? conference.ConferenceId : null;
    }

    private ContactCenterVoiceProviderResult ConsultSuccess(string customerLegId, string consultLegId)
        => new()
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = customerLegId,
            ProviderLegId = consultLegId,
        };

    private static string Read(ContactCenterVoiceAttendedTransferRequest request, string key)
        => request.Metadata is not null && request.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
