using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// What the agent availability report read about one queue or campaign: whether assignment runs for it at all, what is
/// waiting in it, and the decision routing makes for its next waiting item right now.
/// </summary>
internal sealed class AgentAvailabilityQueueContext
{
    /// <summary>
    /// Gets the queue identifier; a campaign's is its virtual campaign queue identifier.
    /// </summary>
    public string QueueId { get; init; }

    /// <summary>
    /// Gets the name to show for the queue or campaign.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets whether this is a campaign's queue.
    /// </summary>
    public bool IsCampaign { get; init; }

    /// <summary>
    /// Gets why assignment hands nobody work from the queue, or <see cref="AgentQueueBlocker.None"/>.
    /// </summary>
    public AgentQueueBlocker Blocker { get; init; }

    /// <summary>
    /// Gets how many items are waiting in the queue.
    /// </summary>
    public int WaitingCount { get; init; }

    /// <summary>
    /// Gets the decision routing made for the queue's next waiting item, with every candidate and the reasons it gave
    /// for each, or <see langword="null"/> when routing was not asked: nothing is waiting, the queue is blocked, or no
    /// agent the report lists passes the availability gate for it.
    /// </summary>
    public ActivityRoutingDecision Decision { get; init; }
}
