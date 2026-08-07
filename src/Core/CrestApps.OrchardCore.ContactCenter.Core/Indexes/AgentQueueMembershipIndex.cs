using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the normalized, query-aligned YesSql index that maps one row per queue an agent is both
/// entitled to and currently signed in to. Routing can select the agents for a queue with a single
/// indexed lookup instead of loading every available agent and filtering membership in memory.
/// </summary>
public sealed class AgentQueueMembershipIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier of the source agent profile.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the queue the agent is entitled to and signed in to.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the current presence state of the agent.
    /// </summary>
    public AgentPresenceStatus PresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent interactions the agent can handle.
    /// </summary>
    public int MaxConcurrentInteractions { get; set; }
}
