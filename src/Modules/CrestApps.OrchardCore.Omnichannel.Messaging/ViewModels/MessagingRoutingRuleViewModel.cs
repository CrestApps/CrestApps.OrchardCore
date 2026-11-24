using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

/// <summary>
/// The editor of a messaging routing rule.
/// </summary>
public class MessagingRoutingRuleViewModel
{
    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the rule's description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the rule's place among the rules.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is tried when a contact starts a conversation.
    /// </summary>
    public bool OnNewConversation { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is tried when a contact replies on a conversation nobody owns.
    /// </summary>
    public bool OnContactReplied { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is tried when an automated send starts a conversation without an agent.
    /// </summary>
    public bool OnStartedWithoutAgent { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is tried again on conversations that have waited.
    /// </summary>
    public bool OnWaiting { get; set; }

    /// <summary>
    /// Gets or sets how many minutes a conversation waits before the rule is tried again.
    /// </summary>
    public int WaitingMinutes { get; set; }

    /// <summary>
    /// Gets or sets the channels the rule takes.
    /// </summary>
    public string[] Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the business addresses the rule takes.
    /// </summary>
    public string[] ServiceAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the queues the rule takes.
    /// </summary>
    public string[] QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the rule takes.
    /// </summary>
    public string[] CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets where the agents come from.
    /// </summary>
    public string CandidateSource { get; set; }

    /// <summary>
    /// Gets or sets the queue whose agents can get the conversation.
    /// </summary>
    public string CandidateQueueId { get; set; }

    /// <summary>
    /// Gets or sets the teams whose members can get the conversation.
    /// </summary>
    public string[] TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles whose users can get the conversation.
    /// </summary>
    public string[] RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the users who can get the conversation.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how an agent is picked.
    /// </summary>
    public string Strategy { get; set; }

    /// <summary>
    /// Gets or sets whether only agents available for messaging can get the conversation.
    /// </summary>
    public bool OnlyAvailableAgents { get; set; }

    /// <summary>
    /// Gets or sets whether agents at their messaging limit are left out.
    /// </summary>
    public bool RespectMaxConcurrent { get; set; }

    /// <summary>
    /// Gets or sets the channels to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableChannels { get; set; } = [];

    /// <summary>
    /// Gets or sets the business addresses to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableServiceAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the queues to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableQueues { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableCampaigns { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableTeams { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets the candidate sources to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableCandidateSources { get; set; } = [];

    /// <summary>
    /// Gets or sets the strategies to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableStrategies { get; set; } = [];
}
