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
                RecordingState.Recording when request.PreviousState == RecordingState.Paused => await ResumeRecordingAsync(callControlId, request.InteractionId, cancellationToken),
                RecordingState.Recording => await StartRecordingAsync(callControlId, cancellationToken),
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

    private async Task<ContactCenterVoiceProviderResult> ResumeRecordingAsync(
        string callControlId,
        string interactionId,
        CancellationToken cancellationToken)
    {
        // Resuming continues the same recording file, so a paused call is resumed rather than given a second recording.
        var resumeResult = await PostRecordingActionAsync(callControlId, "record_resume", body: null, cancellationToken, logFailure: false);

        if (resumeResult.Succeeded)
        {
            return resumeResult;
        }

        // Telnyx no longer holds the paused recording, so the call is recorded afresh rather than left unrecorded.
        _logger.LogWarning(
            "Telnyx had no paused recording to resume for interaction {InteractionId}; starting a new recording.",
            interactionId.SanitizeLogValue());

        return await StartRecordingAsync(callControlId, cancellationToken);
    }

    private Task<ContactCenterVoiceProviderResult> StartRecordingAsync(
        string callControlId,
        CancellationToken cancellationToken)
    {
        // The start is never probed with a resume first: Telnyx answers record_resume with 200 on a call that has no
        // recording at all, so a start that tried resume first took that answer as success and never recorded anything.

        // No client_state: Telnyx would stamp it on every later event of the customer's leg, replacing the state the
        // leg carries. The call.recording.saved webhook names the leg, which is the interaction's provider call, so the
        // recording is traced back to its interaction through that instead.
        var startBody = new Dictionary<string, object>
        {
            ["format"] = TelnyxConstants.Recording.Format,
            ["channels"] = TelnyxConstants.Recording.CallChannels,
        };

        return PostRecordingActionAsync(callControlId, "record_start", startBody, cancellationToken);
    }

    private async Task<ContactCenterVoiceProviderResult> PostRecordingActionAsync(
        string callControlId,
        string action,
        Dictionary<string, object> body,
        CancellationToken cancellationToken,
        bool logFailure = true)
    {
        var result = await _apiClient.PostCallActionAsync(callControlId, action, body, cancellationToken);

        if (!result.Succeeded)
        {
            // A resume that finds nothing to resume is an expected miss on the first start, so the caller says
            // whether a failure here is worth reporting.
            if (logFailure)
            {
                _logger.LogError(
                    "Telnyx rejected recording action {Action} with status code {StatusCode}. Response: {Response}",
                    action,
                    result.StatusCode,
                    result.ErrorBody.SanitizeLogValue());
            }

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
