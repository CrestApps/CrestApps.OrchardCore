using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the Contact Center configuration and live presence for an Orchard user acting as an agent.
/// </summary>
public sealed class AgentProfile : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique name of the agent profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Orchard user this agent profile represents.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the Orchard user this agent profile represents.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent in supervisor and queue views.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent voice interactions the agent can handle.
    /// </summary>
    public int MaxConcurrentInteractions { get; set; } = 1;

    /// <summary>
    /// Gets or sets the current presence state of the agent.
    /// </summary>
    public AgentPresenceStatus PresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional reason code associated with the current presence state.
    /// </summary>
    public string PresenceReason { get; set; }

    /// <summary>
    /// Gets or sets the pending presence state that the system grants after in-flight routing completes.
    /// </summary>
    public AgentPresenceStatus? RequestedPresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the presence state last changed.
    /// </summary>
    public DateTime? PresenceChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent most recently received a routing assignment, used by round-robin routing.
    /// </summary>
    public DateTime? LastAssignedUtc { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is signed in to and can receive work from.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialer campaigns the agent is signed in to.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills the agent can be routed for.
    /// </summary>
    public IList<string> Skills { get; set; } = [];

    /// <summary>
    /// Gets or sets the active reservation identifier when the agent is reserved for an offer.
    /// </summary>
    public string ActiveReservationId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent profile was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
