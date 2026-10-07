using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The recorded calls a user may hear: none, only the calls they took, or everyone's.
/// </summary>
/// <remarks>
/// The call recordings page and every other place that plays a recording decide with this one rule, so a page that
/// lists recordings cannot show a call the recordings page would refuse to play.
/// </remarks>
/// <param name="UserId">The user's identifier.</param>
/// <param name="CanListOwn">Whether the user may hear at least the calls they took.</param>
/// <param name="CanListEveryone">Whether the user may hear every recorded call.</param>
public sealed record CallRecordingAccess(string UserId, bool CanListOwn, bool CanListEveryone)
{
    /// <summary>
    /// Gets the access of a user who may hear no recording.
    /// </summary>
    public static CallRecordingAccess None { get; } = new(null, false, false);

    /// <summary>
    /// Determines whether the user may hear a recording. Whether it can be played at all is <see cref="IsPlayable"/>.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <returns><see langword="true"/> when the user may hear it.</returns>
    public bool CanHear(CallRecording recording)
    {
        if (recording is null || !CanListOwn)
        {
            return false;
        }

        if (CanListEveryone)
        {
            return true;
        }

        // Someone who may only hear their own calls hears only the calls they were the agent on; a call no person
        // took, such as one only an AI voice agent talked on, is nobody's own.
        return !string.IsNullOrEmpty(UserId) &&
            string.Equals(recording.AgentUserId, UserId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether a recording can be played: it landed in the media store and was not erased since.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <returns><see langword="true"/> when it can be played.</returns>
    public static bool IsPlayable(CallRecording recording)
        => recording is not null &&
            recording.StoredUtc.HasValue &&
            !recording.ErasedUtc.HasValue &&
            !string.IsNullOrEmpty(recording.StorageReference);
}
