namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes the current depth of a queue broadcast to the queue's watchers and supervisor dashboards so
/// wallboards and the agent desktop can show live waiting counts.
/// </summary>
public sealed class QueueStatsNotification
{
    /// <summary>
    /// Gets or sets the identifier of the queue the statistics describe.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the number of items currently waiting in the queue.
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the statistics were calculated.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
