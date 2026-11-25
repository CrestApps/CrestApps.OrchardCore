using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// Tries the routing rules again on conversations nobody owns whose contact has waited for a person, so a conversation
/// that found no agent available when the contact wrote goes to one as soon as one is.
/// </summary>
internal sealed class MessagingRoutingRulesSweep
{
    /// <summary>
    /// The most conversations one pass looks at.
    /// </summary>
    public const int BatchSize = 100;

    private readonly ICatalog<MessagingRoutingRule> _rules;
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingRoutingRuleEvaluator _evaluator;
    private readonly IMessagingRealTimeNotifier _notifier;
    private readonly IDistributedLock _distributedLock;
    private readonly MessagingWorkspaceOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRulesSweep"/> class.
    /// </summary>
    /// <param name="rules">The routing rules.</param>
    /// <param name="conversationStore">The conversations.</param>
    /// <param name="evaluator">The routing rules evaluator.</param>
    /// <param name="notifier">The real-time notifier that tells the agent.</param>
    /// <param name="distributedLock">The lock the inbound processor holds on a conversation while it records a message.</param>
    /// <param name="options">The workspace options, read for the conversation lock's timing.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public MessagingRoutingRulesSweep(
        ICatalog<MessagingRoutingRule> rules,
        IMessagingConversationStore conversationStore,
        IMessagingRoutingRuleEvaluator evaluator,
        IMessagingRealTimeNotifier notifier,
        IDistributedLock distributedLock,
        IOptions<MessagingWorkspaceOptions> options,
        IClock clock,
        ILogger<MessagingRoutingRulesSweep> logger)
    {
        _rules = rules;
        _conversationStore = conversationStore;
        _evaluator = evaluator;
        _notifier = notifier;
        _distributedLock = distributedLock;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many conversations were assigned.</returns>
    public async Task<int> RouteWaitingAsync(CancellationToken cancellationToken = default)
    {
        var waitingRules = (await _rules.GetAllAsync(cancellationToken))
            .Where(rule => rule.Enabled && rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.Waiting))
            .ToArray();

        if (waitingRules.Length == 0)
        {
            return 0;
        }

        // The shortest wait any rule has decides which conversations are read; each rule then checks its own wait.
        var minutes = waitingRules.Min(rule => Math.Max(1, rule.WaitingMinutes));
        var now = _clock.UtcNow;
        var waiting = await _conversationStore.GetUnownedAwaitingReplyAsync(now.AddMinutes(-minutes), BatchSize, cancellationToken);
        var assigned = 0;

        foreach (var candidate in waiting)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryAssignAsync(candidate, cancellationToken))
            {
                assigned++;
            }
        }

        if (assigned > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("The routing rules assigned {Count} waiting conversation(s).", assigned);
        }

        return assigned;
    }

    private async Task<bool> TryAssignAsync(MessagingConversation candidate, CancellationToken cancellationToken)
    {
        // The same lock the inbound processor records a message under, so a message arriving now is not overwritten. A
        // conversation whose lock is busy is tried on the next pass.
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            $"MessagingConversation:{candidate.Channel}:{candidate.ServiceAddress}:{candidate.ContactAddress}",
            TimeSpan.FromMilliseconds(500),
            _options.ConversationLockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            // Read again under the lock: an agent may have claimed it, or a message been recorded, since the list was read.
            var conversation = await _conversationStore.FindByIdAsync(candidate.ItemId, cancellationToken);

            if (conversation is null ||
                conversation.Status != ConversationStatus.Open ||
                conversation.IsRead ||
                !RoutingRulesRouter.IsUnowned(conversation))
            {
                return false;
            }

            var previousAgentId = conversation.AssignedAgentId;
            var queueId = conversation.OwnerType == ConversationOwnerType.Queue && !string.IsNullOrEmpty(conversation.OwnerId)
                ? conversation.OwnerId
                : null;

            var match = await _evaluator.AssignAsync(new MessagingRoutingRuleRequest
            {
                Conversation = conversation,
                Trigger = MessagingRoutingRuleTriggers.Waiting,
                QueueId = queueId,
            }, cancellationToken);

            if (match is null)
            {
                return false;
            }

            await _conversationStore.UpdateAsync(conversation, cancellationToken);

            // Tell the agent it is theirs, and light their inbox as a new message would.
            await MessagingAssignmentNotifications.NotifyRoutedAsync(_notifier, conversation, previousAgentId, cancellationToken);
            await _notifier.NewInboundMessageAsync(new MessagingInboundNotification
            {
                ConversationId = conversation.ItemId,
                Channel = conversation.Channel,
                CustomerKey = conversation.GetCustomerKey(),
                ServiceAddress = conversation.ServiceAddress,
                ContactAddress = conversation.ContactAddress,
                Preview = conversation.LastMessagePreview,
                UnreadCount = conversation.UnreadCount,
                ReceivedUtc = conversation.LastMessageUtc ?? _clock.UtcNow,
                AssignedAgentId = conversation.AssignedAgentId,
                OwnerQueueId = queueId,
            }, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The routing rules sweep assigned waiting conversation {ConversationId} to agent {AgentId}.",
                    conversation.ItemId.SanitizeLogValue(),
                    match.AgentId.SanitizeLogValue());
            }

            return true;
        }
    }
}
