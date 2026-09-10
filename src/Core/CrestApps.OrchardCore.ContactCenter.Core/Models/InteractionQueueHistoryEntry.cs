namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a queue transition recorded as part of an interaction's communication history.
/// </summary>
public sealed class InteractionQueueHistoryEntry
{
    /// <summary>
    /// Gets or sets the queue identifier.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction entered the queue.
    /// </summary>
    public DateTime EnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction left the queue.
    /// </summary>
    public DateTime? ExitedUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason the interaction left the queue.
    /// </summary>
    public string ExitReason { get; set; }
}
