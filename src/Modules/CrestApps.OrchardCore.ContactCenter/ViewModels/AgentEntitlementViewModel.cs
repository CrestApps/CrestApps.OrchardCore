using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for a manager-owned agent queue and campaign entitlement configuration.
/// </summary>
public class AgentEntitlementViewModel
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user name the agent profile belongs to. This value is only used when creating
    /// a new agent profile; it cannot be changed once the profile exists.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user's resolved display name.
    /// </summary>
    public string UserDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user-search endpoint used by the agent selector.
    /// </summary>
    public string UserSearchEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is allowed to sign in to.
    /// </summary>
    public IList<string> AllowedQueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the available queues.
    /// </summary>
    public IList<SelectListItem> QueueOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialer campaigns the agent is allowed to sign in to.
    /// </summary>
    public IList<string> AllowedCampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the available dialer campaigns.
    /// </summary>
    public IList<SelectListItem> CampaignOptions { get; set; } = [];
}
