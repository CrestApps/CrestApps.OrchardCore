using System.Collections.Generic;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the agent soft phone sign-in view model.
/// </summary>
public class AgentSoftPhoneViewModel
{
    /// <summary>
    /// Gets or sets the Contact Center real-time hub URL.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the agent profile, when the agent has signed in before.
    /// </summary>
    public AgentProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent can join.
    /// </summary>
    public IList<ActivityQueue> AvailableQueues { get; set; } = [];

    /// <summary>
    /// Gets or sets the queues the agent has selected to sign in to.
    /// </summary>
    public IList<string> SelectedQueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the agent can sign in to for outbound dialer work.
    /// </summary>
    public IList<SelectListItem> CampaignOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected campaign identifiers.
    /// </summary>
    public IList<string> SelectedCampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the enabled agent state reason codes the agent can choose when going not ready.
    /// </summary>
    public IList<AgentStateReasonCode> ReasonCodes { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the agent is currently signed in.
    /// </summary>
    public bool IsSignedIn => Profile is not null &&
        Profile.PresenceStatus != AgentPresenceStatus.Offline &&
        (Profile.QueueIds.Count > 0 || Profile.CampaignIds.Count > 0);
}
