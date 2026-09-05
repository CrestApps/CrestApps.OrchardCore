namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies what a queue should do when an offered reservation expires before the agent accepts it.
/// </summary>
public enum UnansweredOfferAction
{
    /// <summary>
    /// Returns the work item to the queue so it can be offered again.
    /// </summary>
    Requeue,

    /// <summary>
    /// Sends the live voice call to voicemail and removes the work item from the queue.
    /// </summary>
    Voicemail,

    /// <summary>
    /// Rejects the live voice call and removes the work item from the queue.
    /// </summary>
    Reject,
}
