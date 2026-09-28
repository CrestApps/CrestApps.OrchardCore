namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Starts the voicemail recording on a Telnyx call once its greeting has finished playing. Splitting the record
/// start from <c>SendToVoicemailAsync</c> is what keeps the spoken greeting out of the caller's recorded message:
/// the greeting is played first, and only its <c>call.speak.ended</c> / <c>call.playback.ended</c> webhook starts
/// the beep-and-record.
/// </summary>
public interface ITelnyxVoicemailRecordingStarter
{
    /// <summary>
    /// Issues a Telnyx <c>record_start</c> (with a leading beep) on the call, tagging the recording as this
    /// interaction's voicemail so the saved-recording webhook ingests it into the recipient's inbox.
    /// </summary>
    /// <param name="callControlId">The Telnyx call control id of the call to record.</param>
    /// <param name="interactionId">The interaction the voicemail belongs to.</param>
    /// <param name="recipientUserId">The user id of the agent the voicemail was left for, when known.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when Telnyx accepted the record-start request.</returns>
    Task<bool> StartAsync(
        string callControlId,
        string interactionId,
        string recipientUserId,
        CancellationToken cancellationToken = default);
}
