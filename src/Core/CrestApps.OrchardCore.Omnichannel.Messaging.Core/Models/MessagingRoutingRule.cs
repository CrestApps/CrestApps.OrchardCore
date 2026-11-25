using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// A rule that assigns a messaging conversation nobody owns to an agent: when it applies, which conversations it takes,
/// who can get them and how one of them is picked. Enabled rules are tried in <see cref="Order"/>; the first that finds
/// an agent assigns the conversation, and a conversation no rule assigns is left where routing put it.
/// </summary>
public sealed class MessagingRoutingRule : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// The default number of minutes a contact's message waits before the sweep tries the rule again.
    /// </summary>
    public const int DefaultWaitingMinutes = 5;

    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets an optional description of what the rule is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the rule is used.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule's place among the rules. A lower number is tried first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the moments the rule is tried at.
    /// </summary>
    public MessagingRoutingRuleTriggers Triggers { get; set; } = MessagingRoutingRuleTriggers.NewConversation | MessagingRoutingRuleTriggers.ContactReplied | MessagingRoutingRuleTriggers.Waiting;

    /// <summary>
    /// Gets or sets how many minutes the contact's latest message waits for a person before the sweep tries the rule,
    /// when <see cref="Triggers"/> has <see cref="MessagingRoutingRuleTriggers.Waiting"/>.
    /// </summary>
    public int WaitingMinutes { get; set; } = DefaultWaitingMinutes;

    /// <summary>
    /// Gets or sets the channels the rule takes conversations on, such as <c>sms</c>. Empty takes every channel.
    /// </summary>
    public IList<string> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the business addresses (normalized for their channel) the rule takes conversations on. Empty takes
    /// every address.
    /// </summary>
    public IList<string> ServiceAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the queues the rule takes conversations of: the queue that owns the conversation or, for a new one,
    /// the queue its entry point sends it to. Empty takes conversations of any queue and of none.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaigns the rule takes conversations of. Empty takes conversations of any campaign and of none.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets where the agents who can get the conversation come from, one of
    /// <see cref="MessagingRoutingCandidateSources"/>.
    /// </summary>
    public string CandidateSource { get; set; } = MessagingRoutingCandidateSources.QueueSignedIn;

    /// <summary>
    /// Gets or sets the queue whose agents can get the conversation, for the
    /// <see cref="MessagingRoutingCandidateSources.QueueSignedIn"/> and <see cref="MessagingRoutingCandidateSources.QueueMembers"/>
    /// sources. Empty uses the conversation's own queue.
    /// </summary>
    public string CandidateQueueId { get; set; }

    /// <summary>
    /// Gets or sets the teams whose members can get the conversation, for the
    /// <see cref="MessagingRoutingCandidateSources.Teams"/> source.
    /// </summary>
    public IList<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles whose users can get the conversation, for the
    /// <see cref="MessagingRoutingCandidateSources.Roles"/> source.
    /// </summary>
    public IList<string> RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the users who can get the conversation, for the <see cref="MessagingRoutingCandidateSources.Users"/>
    /// source.
    /// </summary>
    public IList<string> UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how one of the agents is picked, one of <see cref="MessagingRoutingRuleStrategies"/>.
    /// </summary>
    public string Strategy { get; set; } = MessagingRoutingRuleStrategies.RoundRobin;

    /// <summary>
    /// Gets or sets whether only agents available for messaging now, with a live workspace session, can get the
    /// conversation.
    /// </summary>
    public bool OnlyAvailableAgents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an agent who already holds as many conversations as their messaging limit allows is left out.
    /// </summary>
    public bool RespectMaxConcurrent { get; set; } = true;

    /// <summary>
    /// Gets or sets when the rule was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the rule was last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// The moments a <see cref="MessagingRoutingRule"/> is tried at.
/// </summary>
[Flags]
public enum MessagingRoutingRuleTriggers
{
    /// <summary>
    /// The rule is never tried.
    /// </summary>
    None = 0,

    /// <summary>
    /// A contact starts a new conversation that its entry point gives to no agent of its own.
    /// </summary>
    NewConversation = 1,

    /// <summary>
    /// A contact writes on an open conversation nobody owns, such as one a workflow or a broadcast started.
    /// </summary>
    ContactReplied = 2,

    /// <summary>
    /// A workflow or another automated send starts a conversation without naming an agent. The rule assigns it at
    /// once, before the contact replies.
    /// </summary>
    StartedWithoutAgent = 4,

    /// <summary>
    /// A conversation nobody owns has waited for a person for <see cref="MessagingRoutingRule.WaitingMinutes"/>. A
    /// background sweep tries the rule again, so a conversation that found nobody available goes to an agent once one is.
    /// </summary>
    Waiting = 8,
}

/// <summary>
/// The places the agents a <see cref="MessagingRoutingRule"/> can pick come from.
/// </summary>
public static class MessagingRoutingCandidateSources
{
    /// <summary>
    /// The agents signed in to a queue now.
    /// </summary>
    public const string QueueSignedIn = "QueueSignedIn";

    /// <summary>
    /// Every agent who may take a queue's work, signed in or not.
    /// </summary>
    public const string QueueMembers = "QueueMembers";

    /// <summary>
    /// The members of teams.
    /// </summary>
    public const string Teams = "Teams";

    /// <summary>
    /// The users in roles.
    /// </summary>
    public const string Roles = "Roles";

    /// <summary>
    /// Named users.
    /// </summary>
    public const string Users = "Users";
}

/// <summary>
/// The ways a <see cref="MessagingRoutingRule"/> picks one of its agents.
/// </summary>
public static class MessagingRoutingRuleStrategies
{
    /// <summary>
    /// The agent who was given a conversation longest ago, so conversations go around the agents in turn.
    /// </summary>
    public const string RoundRobin = "RoundRobin";

    /// <summary>
    /// The agent who holds the fewest open conversations.
    /// </summary>
    public const string LeastOpenConversations = "LeastOpenConversations";

    /// <summary>
    /// The agent with the least work, counting open conversations and live calls, a call weighing as several
    /// conversations.
    /// </summary>
    public const string LeastLoaded = "LeastLoaded";
}
