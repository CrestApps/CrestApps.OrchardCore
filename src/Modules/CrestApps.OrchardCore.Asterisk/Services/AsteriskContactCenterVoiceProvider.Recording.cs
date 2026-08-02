using System.Globalization;
using System.Text;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed partial class AsteriskContactCenterVoiceProvider
{
    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> SetRecordingStateAsync(
        ContactCenterVoiceRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workLease = _workManager.TryEnter(AsteriskConstants.Feature.ContactCenterVoice);

        if (workLease is null)
        {
            return Failure("feature_quiescing", "The Asterisk Contact Center voice provider is temporarily unavailable.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderCallId))
        {
            return Failure("caller_channel_missing", "An Asterisk caller channel id is required to change the recording state.");
        }

        if (string.IsNullOrWhiteSpace(request.InteractionId))
        {
            return Failure("interaction_missing", "An interaction id is required to derive the Asterisk recording name.");
        }

        // Resolve the canonical conversation (mixing) bridge from a binding owned by THIS tenant's store. The bridge
        // persists across transfer and conference, so recording it keeps the whole conversation on one continuous
        // recording. Failing closed when no owning binding exists enforces CC-1: a supervisor can never record a
        // call this tenant does not own.
        var bridgeId = await ResolveConversationBridgeAsync(request.ProviderCallId);

        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return Failure("recording_call_not_owned", "No owned Asterisk conversation bridge was found for the requested recording.");
        }

        var recordingName = CreateRecordingName(request.InteractionId);

        try
        {
            return request.State switch
            {
                RecordingState.Recording => await StartOrResumeRecordingAsync(bridgeId, recordingName, cancellationToken),
                RecordingState.Paused => await PauseRecordingAsync(recordingName, cancellationToken),
                RecordingState.Stopped or RecordingState.None => await StopRecordingAsync(request.InteractionId, recordingName, cancellationToken),
                _ => Failure("recording_state_unsupported", "The requested recording state is not supported."),
            };
        }
        catch (AsteriskAriException ex)
        {
            _logger.LogError(
                ex,
                "Asterisk failed to change the recording state to {RecordingState} for interaction {InteractionId}.",
                request.State,
                request.InteractionId.SanitizeLogValue());

            var outcomeUnknown = IsAmbiguousAriOutcome(ex);

            return new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                OutcomeUnknown = outcomeUnknown,
                ProviderName = TechnicalName,
                ProviderCallId = request.ProviderCallId,
                ErrorCode = outcomeUnknown ? "recording_outcome_unknown" : "recording_failed",
                ErrorMessage = "The Asterisk recording state change could not be confirmed.",
            };
        }
    }

    private async Task<ContactCenterVoiceProviderResult> StartOrResumeRecordingAsync(
        string bridgeId,
        string recordingName,
        CancellationToken cancellationToken)
    {
        var recording = await _ariClient.StartBridgeRecordingAsync(
            bridgeId,
            recordingName,
            AsteriskAriConstants.RecordingFormat,
            cancellationToken);

        // A start that reused an existing paused recording (the deterministic name was already in progress) must be
        // resumed so the request to record actually produces audio. A freshly created recording is already active.
        if (string.Equals(recording?.State, AsteriskAriConstants.RecordingPausedState, StringComparison.OrdinalIgnoreCase))
        {
            await _ariClient.UnpauseBridgeRecordingAsync(recordingName, cancellationToken);
        }

        var format = string.IsNullOrWhiteSpace(recording?.Format)
            ? AsteriskAriConstants.RecordingFormat
            : recording.Format;

        return RecordingSuccess(recordingName, format, durationSeconds: null);
    }

    private async Task<ContactCenterVoiceProviderResult> PauseRecordingAsync(
        string recordingName,
        CancellationToken cancellationToken)
    {
        await _ariClient.PauseBridgeRecordingAsync(recordingName, cancellationToken);

        return RecordingSuccess(recordingName, AsteriskAriConstants.RecordingFormat, durationSeconds: null);
    }

    private async Task<ContactCenterVoiceProviderResult> StopRecordingAsync(
        string interactionId,
        string recordingName,
        CancellationToken cancellationToken)
    {
        var stored = await _ariClient.StopBridgeRecordingAsync(recordingName, cancellationToken);
        var format = string.IsNullOrWhiteSpace(stored?.Format)
            ? AsteriskAriConstants.RecordingFormat
            : stored.Format;

        // Queue the completed recording for durable, encrypted ingestion. The recording has already stopped, so a
        // transient enqueue failure must not fail the stop; the idempotent durable job then owns download-and-store
        // with retry and dead-lettering.
        await EnqueueRecordingIngestAsync(interactionId, recordingName, format, cancellationToken);

        return RecordingSuccess(recordingName, format, stored?.Duration);
    }

    private async Task EnqueueRecordingIngestAsync(
        string interactionId,
        string recordingName,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            await _recordingIngestJobStore.EnqueueAsync(interactionId, recordingName, format, _clock.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue recording {RecordingName} for ingestion.",
                recordingName.SanitizeLogValue());
        }
    }

    private static ContactCenterVoiceProviderResult RecordingSuccess(
        string recordingName,
        string format,
        int? durationSeconds)
    {
        var metadata = new Dictionary<string, string>
        {
            [ContactCenterConstants.RecordingMetadata.ProviderRecordingId] = recordingName,
            [ContactCenterConstants.RecordingMetadata.StorageReference] = recordingName,
            [ContactCenterConstants.RecordingMetadata.Format] = format,
            [ContactCenterConstants.RecordingMetadata.RetrievalPath] = AsteriskAriConstants.StoredRecordingRetrievalPathPrefix + recordingName,
        };

        if (durationSeconds.HasValue)
        {
            metadata[ContactCenterConstants.RecordingMetadata.DurationSeconds] =
                durationSeconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = AsteriskConstants.ProviderTechnicalName,
            Metadata = metadata,
        };
    }

    private async Task<string> ResolveConversationBridgeAsync(string providerCallId)
    {
        // The caller-to-agent connect writes the mixing bridge id onto the agent-leg binding whose PeerChannelId is
        // the caller channel, so a recording request that carries the caller channel id resolves the bridge through
        // that binding. Only bindings persisted in this tenant's store are considered, so ownership is structural.
        var peerBindings = await _channelTenantBindingStore.FindAllByPeerChannelIdAsync(providerCallId);

        var owning = peerBindings.FirstOrDefault(binding => !string.IsNullOrWhiteSpace(binding.BridgeId));

        if (owning is not null)
        {
            return owning.BridgeId;
        }

        // The request may instead carry an id that is itself a bound channel (for example the agent leg), so also try
        // a direct channel lookup before failing closed.
        var direct = await _channelTenantBindingStore.FindByChannelIdAsync(providerCallId);

        return string.IsNullOrWhiteSpace(direct?.BridgeId)
            ? null
            : direct.BridgeId;
    }

    private static string CreateRecordingName(string interactionId)
    {
        // The recording name is derived from the globally unique interaction id (a 26-character generated id), so it
        // is stable across pause/resume/stop and inherently distinct per tenant without an extra prefix lookup.
        return CreateDeterministicAriId(AsteriskAriConstants.RecordingNamePrefix, interactionId);
    }
}
