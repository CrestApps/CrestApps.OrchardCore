using System.Collections.Generic;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the agent workspace sign-in view model.
/// </summary>
public class AgentWorkspaceViewModel
{
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
    /// Gets or sets the skills the agent can select for routing.
    /// </summary>
    public IList<SelectListItem> SkillOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected skill names.
    /// </summary>
    public IList<string> SelectedSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional presence reason submitted by the agent.
    /// </summary>
    public string PresenceReason { get; set; }
}
