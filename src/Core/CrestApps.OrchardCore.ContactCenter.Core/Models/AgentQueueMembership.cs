namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How an agent serves one queue. A bare list of queue identifiers said only that the agent was signed in,
/// so an agent in Sales and Support was always served from whichever queue happened to be stored first, and a
/// caller waiting twenty minutes on the other one was invisible.
/// </summary>
public sealed class AgentQueueMembership
{
    /// <summary>
    /// Gets or sets the queue this membership is for.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets how strongly this agent should be pulled toward this queue, lower first. A supervisor who
    /// marks a queue as an agent's primary is saying it comes first even when another queue has waited longer.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets how long an item must have waited before this agent is offered it. This is how an overflow
    /// queue is expressed: the agent is the backup, so the specialists get their chance first.
    /// </summary>
    public int DelaySeconds { get; set; }
}
