namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Defines the call-control verbs that must pass through the shared authorization boundary.
/// </summary>
public enum CallControlVerb
{
    /// <summary>
    /// Accepts or answers a ringing call.
    /// </summary>
    Accept,

    /// <summary>
    /// Declines or rejects a ringing call.
    /// </summary>
    Decline,

    /// <summary>
    /// Ends an active call.
    /// </summary>
    Hangup,

    /// <summary>
    /// Places a call on hold.
    /// </summary>
    Hold,

    /// <summary>
    /// Resumes a held call.
    /// </summary>
    Resume,

    /// <summary>
    /// Mutes call media.
    /// </summary>
    Mute,

    /// <summary>
    /// Unmutes call media.
    /// </summary>
    Unmute,

    /// <summary>
    /// Sends DTMF digits.
    /// </summary>
    Dtmf,

    /// <summary>
    /// Transfers a call.
    /// </summary>
    Transfer,

    /// <summary>
    /// Merges calls into a conference.
    /// </summary>
    Merge,

    /// <summary>
    /// Starts a new outbound dial.
    /// </summary>
    Dial,

    /// <summary>
    /// Sends a ringing call to voicemail.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Engages a live call as a supervisor.
    /// </summary>
    SupervisorEngage,
}
