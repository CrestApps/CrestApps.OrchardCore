namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What became of the activity of a pre-connect dialer attempt.
/// </summary>
public enum DialerAttemptFinalizationResult
{
    /// <summary>
    /// The attempt is not one the dialer dispositions: the activity is not dialer work, the attempt reached an agent,
    /// or the activity no longer exists.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// The activity was completed with the disposition for the outcome.
    /// </summary>
    Dispositioned,

    /// <summary>
    /// The activity had already finished, so it was left as it was.
    /// </summary>
    AlreadyFinished,

    /// <summary>
    /// No disposition could be applied, because activity management is not enabled or it refused the completion.
    /// </summary>
    NotDispositioned,
}
