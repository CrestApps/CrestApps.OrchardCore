using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// Tries the enabled messaging routing rules, in order, on a conversation nobody owns, and assigns it to the agent the
/// first rule that finds one picks.
/// </summary>
public interface IMessagingRoutingRuleEvaluator
{
    /// <summary>
    /// Tries the rules on a conversation. When one finds an agent, the conversation is assigned to them in memory, with a
    /// history entry naming the rule; the caller saves it and tells the agent.
    /// </summary>
    /// <param name="request">The conversation and the moment the rules are tried at.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rule and the agent, or <see langword="null"/> when no rule assigned the conversation.</returns>
    Task<MessagingRoutingRuleMatch> AssignAsync(MessagingRoutingRuleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A conversation the routing rules are tried on.
/// </summary>
public sealed class MessagingRoutingRuleRequest
{
    /// <summary>
    /// Gets the conversation.
    /// </summary>
    public required MessagingConversation Conversation { get; init; }

    /// <summary>
    /// Gets the moment the rules are tried at. Only rules that apply at it are tried.
    /// </summary>
    public MessagingRoutingRuleTriggers Trigger { get; init; }

    /// <summary>
    /// Gets the queue the conversation belongs to: the one that owns it or, for a new conversation, the one its entry
    /// point sends it to. <see langword="null"/> when it belongs to none.
    /// </summary>
    public string QueueId { get; init; }
}

/// <summary>
/// The rule that assigned a conversation and the agent it went to.
/// </summary>
/// <param name="Rule">The rule.</param>
/// <param name="AgentId">The agent profile id of the agent.</param>
/// <param name="UserId">The agent's user id.</param>
public sealed record MessagingRoutingRuleMatch(MessagingRoutingRule Rule, string AgentId, string UserId);
