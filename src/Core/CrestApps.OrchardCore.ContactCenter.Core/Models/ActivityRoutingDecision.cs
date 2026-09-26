namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of selecting an agent for a queued activity.
/// </summary>
public sealed class ActivityRoutingDecision
{
    /// <summary>
    /// Gets or sets a value indicating whether an agent was selected.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the routed queue.
    /// </summary>
    public ActivityQueue Queue { get; set; }

    /// <summary>
    /// Gets or sets the queue item being assigned.
    /// </summary>
    public QueueItem QueueItem { get; set; }

    /// <summary>
    /// Gets or sets the selected agent, or <see langword="null"/> when no eligible agent was available.
    /// </summary>
    public AgentProfile Agent { get; set; }

    /// <summary>
    /// Gets or sets the human-readable routing outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the scored routing candidates.
    /// </summary>
    public IList<ActivityRoutingCandidate> Candidates { get; set; } = [];
}
