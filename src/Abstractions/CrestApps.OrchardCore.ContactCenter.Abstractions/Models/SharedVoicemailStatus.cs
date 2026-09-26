namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies where a message in a queue's shared voicemail box is in its handling.
/// </summary>
public enum SharedVoicemailStatus
{
    /// <summary>
    /// Nobody has taken the message yet.
    /// </summary>
    New,

    /// <summary>
    /// Somebody said they will handle the message, so the rest of the team leaves it to them.
    /// </summary>
    Claimed,

    /// <summary>
    /// The message has been dealt with.
    /// </summary>
    Resolved,
}
