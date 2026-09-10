using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

public class AgentEntitlementViewModel
{
    public string Id { get; set; }

    public string UserName { get; set; }

    public string UserDisplayName { get; set; }

    public string UserSearchEndpoint { get; set; }

    public IList<string> AllowedQueueIds { get; set; } = [];

    public IList<SelectListItem> QueueOptions { get; set; } = [];

    public IList<string> AllowedCampaignIds { get; set; } = [];

    public IList<SelectListItem> CampaignOptions { get; set; } = [];

    /// <summary>
    /// The skills the agent holds and how well. The queue skill requirements are matched against these.
    /// </summary>
    public IList<AgentSkillViewModel> SkillProficiencies { get; set; } = [];

    public IList<SelectListItem> SkillOptions { get; set; } = [];

    /// <summary>
    /// How the agent serves each allowed queue: a lower priority number is served first, and the delay is how
    /// long a caller waits before this agent is offered work from that queue.
    /// </summary>
    public IList<AgentQueueMembershipViewModel> QueueMemberships { get; set; } = [];
}
