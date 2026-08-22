namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the complete live state of the supervisor dashboard returned by its state endpoint. The
/// client renders the queue tiles and agent board from this snapshot and refreshes it on queue and
/// presence real-time events.
/// </summary>
public sealed class SupervisorDashboardStateViewModel
{
    /// <summary>
    /// Gets or sets the live state of every enabled queue.
    /// </summary>
    public IList<SupervisorQueueViewModel> Queues { get; set; } = [];

    /// <summary>
    /// Gets or sets the live state of every agent.
    /// </summary>
    public IList<SupervisorAgentViewModel> Agents { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of items waiting across all queues.
    /// </summary>
    public int TotalWaiting { get; set; }

    /// <summary>
    /// Gets or sets the number of agents currently available to receive work.
    /// </summary>
    public int AvailableAgents { get; set; }

    /// <summary>
    /// Gets or sets the authoritative server UTC time, used by the client to align local timers.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
