using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Whether one agent can be given work from one queue or campaign right now, and every reason they cannot.
/// </summary>
internal sealed class AgentQueueEligibility
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
    /// Gets whether a manager entitled the agent to the queue or campaign.
    /// </summary>
    public bool Entitled { get; init; }

    /// <summary>
    /// Gets whether the agent is signed in to the queue or campaign.
    /// </summary>
    public bool SignedIn { get; init; }

    /// <summary>
    /// Gets why the whole queue hands nobody work, or <see cref="AgentQueueBlocker.None"/>.
    /// </summary>
    public AgentQueueBlocker Blocker { get; init; }

    /// <summary>
    /// Gets every reason the availability gate keeps the agent out of the queue's routing.
    /// </summary>
    public AgentRoutingIneligibility Gate { get; init; }

    /// <summary>
    /// Gets how many interactions the agent handles now.
    /// </summary>
    public int ActiveInteractionCount { get; init; }

    /// <summary>
    /// Gets how many interactions the agent may handle at once.
    /// </summary>
    public int Capacity { get; init; }

    /// <summary>
    /// Gets how many items are waiting in the queue.
    /// </summary>
    public int WaitingCount { get; init; }

    /// <summary>
    /// Gets whether routing was asked about the queue's next waiting item with this agent among its candidates.
    /// </summary>
    public bool RoutingEvaluated { get; init; }

    /// <summary>
    /// Gets whether routing's strategies left the agent eligible for the next waiting item, or <see langword="null"/>
    /// when routing was not asked, or did not have the agent among its candidates.
    /// </summary>
    public bool? RoutingEligible { get; init; }

    /// <summary>
    /// Gets whether routing picks this agent for the next waiting item.
    /// </summary>
    public bool NextInLine { get; init; }

    /// <summary>
    /// Gets the reasons routing gave for this agent, in its own words.
    /// </summary>
    public IReadOnlyList<string> RoutingReasons { get; init; } = [];

    /// <summary>
    /// Gets whether the agent can be given work from the queue right now: nothing blocks the queue, the availability
    /// gate lets the agent through, and, when routing was asked about the next waiting item, it left the agent eligible.
    /// </summary>
    public bool Eligible
        => Blocker == AgentQueueBlocker.None &&
            Gate == AgentRoutingIneligibility.None &&
            (!RoutingEvaluated || RoutingEligible == true);
}
