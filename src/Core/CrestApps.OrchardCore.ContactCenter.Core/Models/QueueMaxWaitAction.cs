namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What happens to a caller who has waited as long as the queue is willing to make anybody wait. Doing nothing
/// is not on the list: a caller left on hold indefinitely is the failure this setting exists to prevent.
/// </summary>
public enum QueueMaxWaitAction
{
    /// <summary>
    /// Keep waiting. The queue has not set a limit.
    /// </summary>
    None,

    /// <summary>
    /// Send the caller to voicemail.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Take the caller's number and call them back, keeping their place in line.
    /// </summary>
    Callback,

    /// <summary>
    /// Move the caller to the next queue in the overflow chain.
    /// </summary>
    Overflow,

    /// <summary>
    /// Transfer the caller to an approved external destination.
    /// </summary>
    Transfer,

    /// <summary>
    /// Play a closing message and end the call, which at least tells the caller where they stand.
    /// </summary>
    HangUpWithMessage,
}
