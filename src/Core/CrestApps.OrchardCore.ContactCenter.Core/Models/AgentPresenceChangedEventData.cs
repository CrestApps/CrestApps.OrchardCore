using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes an auditable agent presence transition and the memberships that were active after it.
/// </summary>
public sealed class AgentPresenceChangedEventData
{
    /// <summary>
    /// Gets or sets the presence status before the transition.
    /// </summary>
    public AgentPresenceStatus PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the presence status after the transition.
    /// </summary>
    public AgentPresenceStatus CurrentStatus { get; set; }

    /// <summary>
    /// Gets or sets the presence status requested for after active work completes.
    /// </summary>
    public AgentPresenceStatus? RequestedStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional reason associated with the current status.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is signed in to after the transition.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the agent is signed in to after the transition.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the transition occurred.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
