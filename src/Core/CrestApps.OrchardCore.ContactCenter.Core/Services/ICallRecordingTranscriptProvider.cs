using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Supplies the transcript shown beside a call recording, placed on the recording's timeline so each line can be
/// played from where it was said. Several providers may be registered; the first one that has a transcript for the
/// recording supplies it.
/// </summary>
public interface ICallRecordingTranscriptProvider
{
    /// <summary>
    /// Gets the transcript of a recorded call.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The transcript, or <see langword="null"/> when this provider has none for the call.</returns>
    Task<CallRecordingTranscript> GetTranscriptAsync(CallRecording recording, CancellationToken cancellationToken = default);
}
