using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What became of an attempt to erase a call recording.
/// </summary>
public enum CallRecordingErasureOutcome
{
    /// <summary>
    /// The recording was erased.
    /// </summary>
    Erased,

    /// <summary>
    /// The recording's interaction is under legal hold, so nothing was erased.
    /// </summary>
    LegalHold,

    /// <summary>
    /// The recording's media could not be deleted, so nothing was erased.
    /// </summary>
    Failed,
}

/// <summary>
/// Reads, audits and erases the recorded calls a user may hear, by the rules of the call recordings page: a recording
/// the user may not hear, or that cannot be played, is reported as one that is not there; opening one is written to
/// its interaction's recording-access trail; an interaction's recording is erased through recording governance, which
/// refuses one under legal hold. The call recordings page and the call recording AI tools both go through it, so
/// neither can skip the audit or the own-calls restriction.
/// </summary>
public sealed class CallRecordingReader
{
    private readonly ICallRecordingStore _store;
    private readonly IRecordingAccessGovernanceService _governance;
    private readonly IEnumerable<ICallRecordingTranscriptProvider> _transcriptProviders;
    private readonly IInteractionManager _interactionManager;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingReader"/> class.
    /// </summary>
    /// <param name="store">The call recordings catalog.</param>
    /// <param name="governance">The recording governance that audits every playback and erases interaction recordings.</param>
    /// <param name="transcriptProviders">The sources of a call's transcript.</param>
    /// <param name="interactionManager">The interaction manager, read for a recording's legal hold.</param>
    /// <param name="mediaStores">The encrypted recording media store, when telephony registered one.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CallRecordingReader(
        ICallRecordingStore store,
        IRecordingAccessGovernanceService governance,
        IEnumerable<ICallRecordingTranscriptProvider> transcriptProviders,
        IInteractionManager interactionManager,
        IEnumerable<IRecordingMediaStore> mediaStores,
        IClock clock,
        ILogger<CallRecordingReader> logger)
    {
        _store = store;
        _governance = governance;
        _transcriptProviders = transcriptProviders;
        _interactionManager = interactionManager;
        _mediaStore = mediaStores.LastOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Finds a recording the user may hear and that can be played. A recording the user may not hear, or that cannot
    /// be played, is reported as one that is not there, so its existence is not disclosed.
    /// </summary>
    /// <param name="id">The recording identifier.</param>
    /// <param name="access">The user's access.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The recording, or <see langword="null"/>.</returns>
    public async Task<CallRecording> FindPlayableAsync(string id, CallRecordingAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var recording = await _store.FindByIdAsync(id, cancellationToken);

        return CallRecordingAccess.IsPlayable(recording) && access.CanHear(recording)
            ? recording
            : null;
    }

    /// <summary>
    /// Records that a user opened a recording. An interaction's recording is listened to on the interaction's own
    /// recording-access trail; a recording that belongs to no interaction has no trail to join, so the access is logged.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="access">The user's access.</param>
    /// <param name="purpose">Why the recording was opened.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes once the access is recorded.</returns>
    public async Task AuditAccessAsync(CallRecording recording, CallRecordingAccess access, string purpose, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(access);

        var actor = access.CanListEveryone && !string.Equals(recording.AgentUserId, access.UserId, StringComparison.Ordinal)
            ? ContactCenterActor.Supervisor(access.UserId)
            : ContactCenterActor.Agent(access.UserId);

        if (!string.IsNullOrEmpty(recording.InteractionId) &&
            await _governance.RecordAccessAsync(recording.InteractionId, actor, purpose, cancellationToken))
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User '{UserId}' opened call recording '{RecordingId}' ({Source}, agent '{AgentUserId}') for {Purpose}.",
                access.UserId.SanitizeLogValue(),
                recording.ItemId.SanitizeLogValue(),
                recording.Source,
                recording.AgentUserId.SanitizeLogValue(),
                purpose);
        }
    }

    /// <summary>
    /// Reads a recording's transcript from the first source that has one.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The transcript, or <see langword="null"/> when no source has one.</returns>
    public async Task<CallRecordingTranscript> GetTranscriptAsync(CallRecording recording, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording);

        foreach (var provider in _transcriptProviders)
        {
            try
            {
                var transcript = await provider.GetTranscriptAsync(recording, cancellationToken);

                if (transcript is not null && transcript.Phrases.Count > 0)
                {
                    return transcript;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The recording still plays without its transcript.
                _logger.LogWarning(ex, "The transcript of call recording '{RecordingId}' could not be read.", recording.ItemId.SanitizeLogValue());
            }
        }

        return null;
    }

    /// <summary>
    /// Erases a recording: its media is deleted and it is no longer listed. An interaction's recording is erased
    /// through recording governance, which refuses a recording under legal hold and audits the erasure. The writes are
    /// durable and are not cancelled with the request.
    /// </summary>
    /// <param name="recording">A recording the user may hear, from <see cref="FindPlayableAsync"/>.</param>
    /// <param name="access">The user's access.</param>
    /// <param name="reason">Why the recording is erased, kept on the audit trail.</param>
    /// <returns>What became of the erasure.</returns>
    public async Task<CallRecordingErasureOutcome> EraseAsync(CallRecording recording, CallRecordingAccess access, string reason)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!string.IsNullOrEmpty(recording.InteractionId))
        {
            var decision = await _governance.EraseAsync(
                recording.InteractionId,
                ContactCenterActor.Supervisor(access.UserId),
                reason,
                CancellationToken.None);

            if (decision.Erased)
            {
                // The erasure event takes every recording of the interaction off the page; this one goes at once.
                recording.ErasedUtc = _clock.UtcNow;
                await _store.UpdateAsync(recording, CancellationToken.None);

                LogErased(recording, access);

                return CallRecordingErasureOutcome.Erased;
            }

            // A hold is checked on the interaction too: it no longer naming a recording does not lift it.
            var interaction = await _interactionManager.FindByIdAsync(recording.InteractionId, CancellationToken.None);

            if (decision.DenyReasonCode == ContactCenterConstants.RecordingErasureDenyReason.LegalHold ||
                interaction?.RecordingLegalHold == true)
            {
                return CallRecordingErasureOutcome.LegalHold;
            }

            // The interaction no longer names a recording: it was erased already, or this one was never attached to
            // it. The entry's own media is erased below.
        }

        if (_mediaStore is null || !await _mediaStore.DeleteAsync(recording.StorageReference, CancellationToken.None))
        {
            return CallRecordingErasureOutcome.Failed;
        }

        recording.ErasedUtc = _clock.UtcNow;
        await _store.UpdateAsync(recording, CancellationToken.None);

        LogErased(recording, access);

        return CallRecordingErasureOutcome.Erased;
    }

    private void LogErased(CallRecording recording, CallRecordingAccess access)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User '{UserId}' erased call recording '{RecordingId}' (interaction '{InteractionId}').",
                access.UserId.SanitizeLogValue(),
                recording.ItemId.SanitizeLogValue(),
                recording.InteractionId.SanitizeLogValue());
        }
    }
}
