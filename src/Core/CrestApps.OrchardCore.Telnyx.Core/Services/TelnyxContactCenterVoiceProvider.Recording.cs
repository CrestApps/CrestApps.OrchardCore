using System.Net.Http.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

public sealed partial class TelnyxContactCenterVoiceProvider
{
    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> SetRecordingStateAsync(
        ContactCenterVoiceRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(TelnyxConstants.ContactCenterVoiceWorkPartition);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Telnyx Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_call_missing", "A Telnyx call control id is required to change the recording state.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to correlate the Telnyx recording.");
        }

        if (!_options.IsConfigured)
        {
            return Failure("provider_unavailable", "The Telnyx telephony provider is not configured.");
        }

        var callControlId = request.ProviderCallId.Trim();

        try
        {
            return request.State switch
            {
                RecordingState.Recording => await StartOrResumeRecordingAsync(callControlId, request.InteractionId, cancellationToken),
                RecordingState.Paused => await PostRecordingActionAsync(callControlId, "record_pause", body: null, cancellationToken),
                RecordingState.Stopped or RecordingState.None => await PostRecordingActionAsync(callControlId, "record_stop", body: null, cancellationToken),
                _ => Failure("recording_state_unsupported", "The requested recording state is not supported."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telnyx failed to change the recording state to {RecordingState} for interaction {InteractionId}.",
                request.State,
                request.InteractionId.SanitizeLogValue());

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = true,
                ProviderName = TechnicalName,
                ProviderCallId = callControlId,
                ErrorCode = "recording_outcome_unknown",
                ErrorMessage = "The Telnyx recording state change could not be confirmed.",
            };
        }
    }

    private async Task<ContactCenterVoiceProviderResult> StartOrResumeRecordingAsync(
        string callControlId,
        string interactionId,
        CancellationToken cancellationToken)
    {
        // A "record" request is either the first start of a recording or a resume of one that was paused. Resuming
        // continues the SAME recording file, so it is attempted first: only when there is no paused recording to
        // resume is a fresh recording started. Trying resume first (rather than start first) guarantees a resume
        // never accidentally starts a second, parallel recording of the same call.
        using var client = CreateClient();

        using (var resumeContent = JsonContent.Create(new Dictionary<string, object>(), options: TelnyxJsonSerializerOptions.Default))
        using (var resumeResponse = await client.PostAsync(
            $"calls/{Uri.EscapeDataString(callControlId)}/actions/record_resume",
            resumeContent,
            cancellationToken))
        {
            if (resumeResponse.IsSuccessStatusCode)
            {
                return RecordingSuccess(callControlId);
            }

            // A resume miss is expected on the initial start (there is nothing to resume yet), so it is not an
            // error; fall through to start a new recording.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Telnyx had no paused recording to resume for interaction {InteractionId}; starting a new recording.",
                    interactionId.SanitizeLogValue());
            }
        }

        // The recording carries the interaction as client_state so the call.recording.saved webhook can be
        // correlated back to the interaction that owns it and its media ingested into the encrypted store.
        var startBody = new Dictionary<string, object>
        {
            ["format"] = TelnyxConstants.Recording.Format,
            ["channels"] = "single",
            ["client_state"] = TelnyxRecordingClientState.ForInteraction(interactionId).ToClientState(),
        };

        return await PostRecordingActionAsync(callControlId, "record_start", startBody, cancellationToken);
    }

    private async Task<ContactCenterVoiceProviderResult> PostRecordingActionAsync(
        string callControlId,
        string action,
        Dictionary<string, object> body,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        using var content = JsonContent.Create(body ?? new Dictionary<string, object>(), options: TelnyxJsonSerializerOptions.Default);
        using var response = await client.PostAsync(
            $"calls/{Uri.EscapeDataString(callControlId)}/actions/{action}",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Telnyx rejected recording action {Action} with status code {StatusCode}. Response: {Response}",
                action,
                response.StatusCode,
                (await SafeReadContentAsync(response, cancellationToken)).SanitizeLogValue());

            return Failure("recording_failed", "The Telnyx recording state change was not applied.");
        }

        return RecordingSuccess(callControlId);
    }

    private ContactCenterVoiceProviderResult RecordingSuccess(string callControlId)
        => new()
        {
            Succeeded = true,
            ProviderName = TechnicalName,
            ProviderCallId = callControlId,
            // The Telnyx recording id (and therefore the storage reference) is only known when the recording is
            // saved, so the retrieval handle is stamped onto the interaction from the call.recording.saved
            // webhook rather than here. Only the format is known up front.
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.RecordingMetadata.Format] = TelnyxConstants.Recording.Format,
            },
        };
}
