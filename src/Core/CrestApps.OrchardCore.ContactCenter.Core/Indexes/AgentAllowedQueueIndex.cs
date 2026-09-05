using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Maps one row per queue an agent is entitled to (a member of), independent of voice presence or sign-in, so
/// channels that route without a sign-in gate — such as routed SMS — can select a queue's members with a single
/// indexed lookup instead of loading every agent and filtering membership in memory. Distinct from
/// <see cref="AgentQueueMembershipIndex"/>, which only rows the queues an agent is also signed in to.
/// </summary>
public sealed class AgentAllowedQueueIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier of the source agent profile.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the queue the agent is entitled to, stored lower-cased for portable case-insensitive matching.
    /// </summary>
    public string QueueId { get; set; }
}
