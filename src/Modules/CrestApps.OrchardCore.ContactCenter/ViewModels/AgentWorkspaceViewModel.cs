using System.Collections.Generic;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

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
    /// Gets or sets the campaign identifiers, comma separated, the agent is signed in to.
    /// </summary>
    public string CampaignIds { get; set; }
}
