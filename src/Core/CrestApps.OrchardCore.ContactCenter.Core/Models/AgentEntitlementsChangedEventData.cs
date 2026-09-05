namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes manager-owned agent entitlement changes and any live memberships removed by them.
/// </summary>
public sealed class AgentEntitlementsChangedEventData
{
    /// <summary>
    /// Gets or sets the queues the agent is allowed to join.
    /// </summary>
    public IList<string> AllowedQueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the agent is allowed to join.
    /// </summary>
    public IList<string> AllowedCampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the live queue memberships removed by this change.
    /// </summary>
    public IList<string> RemovedQueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the live campaign memberships removed by this change.
    /// </summary>
    public IList<string> RemovedCampaignIds { get; set; } = [];
}
