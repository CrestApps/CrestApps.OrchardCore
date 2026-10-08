namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// One hop in a queue's overflow chain: where a caller goes, and how long they must have waited first. A single
/// overflow queue could only express "somewhere else eventually"; a chain expresses the escalation an operations
/// team actually runs, where a caller widens from specialists to a general pool as they wait.
/// </summary>
public sealed class QueueOverflowTarget
{
    /// <summary>
    /// Gets or sets the queue the caller moves to.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets how long the caller must have waited before this hop applies.
    /// </summary>
    public int AfterSeconds { get; set; }
}
