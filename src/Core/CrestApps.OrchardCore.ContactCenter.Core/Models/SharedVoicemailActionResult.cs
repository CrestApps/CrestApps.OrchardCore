namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of something a user asked to do with a message in a queue's shared voicemail box.
/// </summary>
public sealed class SharedVoicemailActionResult
{
    /// <summary>
    /// Gets how the request ended.
    /// </summary>
    public SharedVoicemailActionStatus Status { get; private init; }

    /// <summary>
    /// Gets the message the request was about, as it is after the request, when the user may see it.
    /// </summary>
    public SharedVoicemail Voicemail { get; private init; }

    /// <summary>
    /// Gets the callback the request scheduled, when it scheduled one.
    /// </summary>
    public CallbackRequest Callback { get; private init; }

    /// <summary>
    /// Gets the recording the request opened, when it opened one. The caller owns and disposes the stream.
    /// </summary>
    public Stream Recording { get; private init; }

    /// <summary>
    /// Gets a stable code naming why the request ended without doing what was asked, one of
    /// <see cref="SharedVoicemailReasons"/>, when there is more to say than <see cref="Status"/>.
    /// </summary>
    public string ReasonCode { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the request did what was asked.
    /// </summary>
    public bool Succeeded => Status == SharedVoicemailActionStatus.Succeeded;

    /// <summary>
    /// Creates the result of a request that did what was asked.
    /// </summary>
    /// <param name="voicemail">The message as it is after the request.</param>
    /// <param name="callback">The callback the request scheduled, when it scheduled one.</param>
    /// <param name="recording">The recording the request opened, when it opened one.</param>
    /// <returns>The result.</returns>
    public static SharedVoicemailActionResult Success(SharedVoicemail voicemail, CallbackRequest callback = null, Stream recording = null)
        => new()
        {
            Status = SharedVoicemailActionStatus.Succeeded,
            Voicemail = voicemail,
            Callback = callback,
            Recording = recording,
        };

    /// <summary>
    /// Creates the result of a request that ended without doing what was asked.
    /// </summary>
    /// <param name="status">Why the request ended.</param>
    /// <param name="voicemail">The message, when the user may see it.</param>
    /// <param name="reasonCode">One of <see cref="SharedVoicemailReasons"/>, when there is more to say.</param>
    /// <returns>The result.</returns>
    public static SharedVoicemailActionResult Failure(SharedVoicemailActionStatus status, SharedVoicemail voicemail = null, string reasonCode = null)
        => new()
        {
            Status = status,
            Voicemail = voicemail,
            ReasonCode = reasonCode,
        };
}

/// <summary>
/// How a request about a message in a queue's shared voicemail box ended.
/// </summary>
public enum SharedVoicemailActionStatus
{
    /// <summary>
    /// The request did what was asked.
    /// </summary>
    Succeeded,

    /// <summary>
    /// There is no such message, or the user may not see it. The two are not told apart, so a user cannot learn that
    /// another team's message exists.
    /// </summary>
    NotFound,

    /// <summary>
    /// The user may see the message but may not do this to it: it is somebody else's claim, or deleting it needs a
    /// stronger permission.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The message is not in a state that allows the request: it was already dealt with, it is claimed by somebody
    /// else, its caller left no number to call back, or its recording is under legal hold.
    /// </summary>
    Conflict,

    /// <summary>
    /// What the request needs is not available: no recording was captured or ingested yet, or callbacks are not
    /// enabled for the tenant.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The stable codes that say why a request about a message in a queue's shared voicemail box did not do what was asked.
/// </summary>
public static class SharedVoicemailReasons
{
    /// <summary>
    /// Somebody else has claimed the message.
    /// </summary>
    public const string ClaimedByAnotherUser = "claimedByAnotherUser";

    /// <summary>
    /// The message was already dealt with, and has to be returned to the team before it is worked again.
    /// </summary>
    public const string AlreadyResolved = "alreadyResolved";

    /// <summary>
    /// Deleting a message needs <see cref="ContactCenterPermissions.ManageSharedVoicemail"/>.
    /// </summary>
    public const string ManagePermissionRequired = "managePermissionRequired";

    /// <summary>
    /// The caller left no number to call back.
    /// </summary>
    public const string NoCallerNumber = "noCallerNumber";

    /// <summary>
    /// Callbacks are not enabled for the tenant.
    /// </summary>
    public const string CallbacksUnavailable = "callbacksUnavailable";

    /// <summary>
    /// The recording is under legal hold and cannot be erased.
    /// </summary>
    public const string LegalHold = "legalHold";

    /// <summary>
    /// No recording can be played: the caller hung up before leaving one, it has not finished ingesting, or recording
    /// governance is not enabled.
    /// </summary>
    public const string NoRecording = "noRecording";
}
