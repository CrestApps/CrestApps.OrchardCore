using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Gives a contact's message on a conversation nobody owns to an agent through the routing rules: a new conversation
/// whose entry point names no agent of its own, and a reply on an open conversation that is unassigned or waiting in a
/// queue's pool, such as one a workflow started with a text. It runs before <see cref="ExistingConversationRouter"/>
/// and <see cref="RoutedQueueRouter"/>, and declines when no rule assigns the conversation, so routing goes on as it
/// would without rules.
/// </summary>
internal sealed class RoutingRulesRouter : IMessagingInboundRouter
{
    private readonly IMessagingRoutingRuleEvaluator _evaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingRulesRouter"/> class.
    /// </summary>
    /// <param name="evaluator">The routing rules.</param>
    public RoutingRulesRouter(IMessagingRoutingRuleEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    /// <inheritdoc/>
    public int Order => 150;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != MessagingRoutingTrigger.Inbound)
        {
            return false;
        }

        var conversation = context.Conversation;
        MessagingRoutingRuleTriggers trigger;
        string queueId;

        if (context.IsNewConversation)
        {
            var routing = context.Routing;

            // An entry point that sends the number's messages to one agent already named the owner.
            if (routing is { TargetType: ConversationRouteTargetType.Agent } && !string.IsNullOrEmpty(routing.TargetId))
            {
                return false;
            }

            trigger = MessagingRoutingRuleTriggers.NewConversation;
            queueId = routing is { TargetType: ConversationRouteTargetType.Queue } && !string.IsNullOrEmpty(routing.TargetId)
                ? routing.TargetId
                : null;
        }
        else
        {
            if (!IsUnowned(conversation))
            {
                return false;
            }

            trigger = MessagingRoutingRuleTriggers.ContactReplied;
            queueId = conversation.OwnerType == ConversationOwnerType.Queue && !string.IsNullOrEmpty(conversation.OwnerId)
                ? conversation.OwnerId
                : null;
        }

        var match = await _evaluator.AssignAsync(new MessagingRoutingRuleRequest
        {
            Conversation = conversation,
            Trigger = trigger,
            QueueId = queueId,
        }, cancellationToken);

        if (match is null)
        {
            return false;
        }

        // The contact wrote again on a thread that was closed: it is open again, as it would be without rules.
        if (conversation.Status is ConversationStatus.Closed or ConversationStatus.Snoozed)
        {
            conversation.Status = ConversationStatus.Open;
        }

        return true;
    }

    /// <summary>
    /// Determines whether nobody owns a conversation: no agent is assigned and it is not a person's own thread.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <returns><see langword="true"/> when the conversation is unassigned or waiting in a queue's pool.</returns>
    public static bool IsUnowned(MessagingConversation conversation)
        => conversation is not null &&
            string.IsNullOrEmpty(conversation.AssignedAgentId) &&
            conversation.AssignmentStatus is ConversationAssignmentStatus.Unassigned or ConversationAssignmentStatus.Pooled &&
            (conversation.OwnerType == ConversationOwnerType.Queue || string.IsNullOrEmpty(conversation.OwnerId));
}
