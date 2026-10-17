namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Names why a whole queue or campaign hands nobody work, whoever the agent is. Each value mirrors a check assignment
/// makes before it looks at any agent.
/// </summary>
internal enum AgentQueueBlocker
{
    /// <summary>
    /// Nothing about the queue stops assignment.
    /// </summary>
    None,

    /// <summary>
    /// The queue no longer exists.
    /// </summary>
    Missing,

    /// <summary>
    /// The queue is turned off.
    /// </summary>
    Disabled,

    /// <summary>
    /// The queue's business hours are closed.
    /// </summary>
    Closed,
}
