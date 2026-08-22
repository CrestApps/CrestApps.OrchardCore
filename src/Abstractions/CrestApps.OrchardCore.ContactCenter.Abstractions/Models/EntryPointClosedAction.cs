namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies what an inbound entry point does with a call while it is closed (outside business hours or
/// when no agent can take the call).
/// </summary>
public enum EntryPointClosedAction
{
    /// <summary>
    /// Keep the call in the target queue until an agent is available or the queue reopens.
    /// </summary>
    HoldInQueue,

    /// <summary>
    /// Send the caller to voicemail.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Route the call to the overflow queue.
    /// </summary>
    Overflow,

    /// <summary>
    /// Reject the call with an after-hours message.
    /// </summary>
    Reject,
}
