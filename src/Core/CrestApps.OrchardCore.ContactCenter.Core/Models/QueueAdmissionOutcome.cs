namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How a queue answered a request to admit a new caller.
/// </summary>
public enum QueueAdmissionOutcome
{
    /// <summary>
    /// The caller may wait in the queue that was asked.
    /// </summary>
    Admitted,

    /// <summary>
    /// The queue is full and the caller is admitted to an overflow queue instead.
    /// </summary>
    Overflowed,

    /// <summary>
    /// The queue is full and the caller is to be sent to voicemail rather than made to wait.
    /// </summary>
    Voicemail,
}
