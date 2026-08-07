namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies how a queue handles waiting work while its business-hours calendar reports that the queue is closed.
/// </summary>
public enum QueueAfterHoursAction
{
    /// <summary>
    /// Keeps waiting items in the queue and pauses assignment until the queue is open again.
    /// </summary>
    HoldInQueue,

    /// <summary>
    /// Moves waiting items to the configured overflow queue while the queue is closed.
    /// </summary>
    Overflow,
}
