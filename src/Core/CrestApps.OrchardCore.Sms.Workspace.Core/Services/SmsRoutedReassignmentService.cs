using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// Default <see cref="ISmsRoutedReassignmentService"/>. When a routed conversation is not picked up within the
/// grace window, it is first re-routed to another eligible agent (excluding the one who did not pick it up); when
/// no other agent is available it is returned to the queue's shared pool so any member can claim it. Both paths
/// re-light the appropriate inbox.
/// </summary>
public sealed class SmsRoutedReassignmentService : ISmsRoutedReassignmentService
{
    /// <summary>
    /// The default time a routed conversation may sit unpicked before it is re-routed or returned to the shared
    /// pool. Overridable through <see cref="SmsRoutedDistributionOptions.PickupGraceMinutes"/>.
    /// </summary>
    public static readonly TimeSpan PickupGraceWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The default maximum number of times a routed conversation is re-routed before it falls back to the shared
    /// pool. Overridable through <see cref="SmsRoutedDistributionOptions.MaxReassignmentAttempts"/>.
    /// </summary>
    public const int MaxReassignmentAttempts = 2;

    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsRoutingStrategy _routingStrategy;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly TimeSpan _pickupGraceWindow;
    private readonly int _maxReassignmentAttempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsRoutedReassignmentService"/> class.
    /// </summary>
    public SmsRoutedReassignmentService(
        ISmsConversationStore conversationStore,
        ISmsRoutingStrategy routingStrategy,
        ISmsRealTimeNotifier notifier,
        IClock clock,
        IOptions<SmsRoutedDistributionOptions> options,
        ILogger<SmsRoutedReassignmentService> logger)
    {
        _conversationStore = conversationStore;
        _routingStrategy = routingStrategy;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;

        var value = options.Value;
        _pickupGraceWindow = value.PickupGraceMinutes > 0 ? TimeSpan.FromMinutes(value.PickupGraceMinutes) : PickupGraceWindow;
        _maxReassignmentAttempts = Math.Max(0, value.MaxReassignmentAttempts);
    }

    /// <inheritdoc/>
    public async Task<int> ReassignStaleAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _conversationStore.GetRoutedAwaitingPickupAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var cutoff = now - _pickupGraceWindow;
        var moved = 0;

        foreach (var conversation in candidates)
        {
            if (conversation.AssignedUtc is null || conversation.AssignedUtc > cutoff)
            {
                continue;
            }

            var queueId = conversation.OwnerId;
            var previousAgentId = conversation.AssignedAgentId;

            // Prefer re-routing to another eligible agent, excluding the one who did not pick it up — but only up to
            // a bounded number of attempts, so a thread ignored by successive agents cannot bounce forever. Beyond
            // that, or when no other agent is available, it falls back to the queue's shared pool.
            var nextAgentId = conversation.ReassignmentAttempts < _maxReassignmentAttempts
                ? await _routingStrategy.SelectAgentAsync(queueId, excludeAgentId: previousAgentId, cancellationToken)
                : null;

            if (!string.IsNullOrEmpty(nextAgentId))
            {
                conversation.AssignedAgentId = nextAgentId;
                conversation.AssignmentStatus = SmsConversationAssignmentStatus.Assigned;
                conversation.AssignedUtc = now;
                conversation.ReassignmentAttempts++;
                conversation.ModifiedUtc = now;

                await _conversationStore.UpdateAsync(conversation, cancellationToken);

                // Offer it to the newly-assigned agent's inbox.
                await NotifyAsync(conversation, nextAgentId, queueId, now, cancellationToken);
            }
            else
            {
                // Return the thread to the queue's shared pool: clear the assignment so any member can claim it.
                conversation.AssignmentStatus = SmsConversationAssignmentStatus.Pooled;
                conversation.AssignedAgentId = null;
                conversation.AssignedUtc = null;
                conversation.ReassignmentAttempts = 0;
                conversation.ModifiedUtc = now;

                await _conversationStore.UpdateAsync(conversation, cancellationToken);

                // Re-light the queue inbox so a member picks it up.
                await NotifyAsync(conversation, assignedAgentId: null, queueId, now, cancellationToken);
            }

            moved++;
        }

        if (moved > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Re-routed or re-pooled {Count} unpicked routed SMS conversation(s).", moved);
        }

        return moved;
    }

    private Task NotifyAsync(Models.SmsConversation conversation, string assignedAgentId, string queueId, DateTime now, CancellationToken cancellationToken)
        => _notifier.NewInboundMessageAsync(new SmsInboundNotification
        {
            ConversationId = conversation.ItemId,
            ServiceAddress = conversation.ServiceAddress,
            ContactAddress = conversation.ContactAddress,
            Preview = conversation.LastMessagePreview,
            UnreadCount = conversation.UnreadCount,
            ReceivedUtc = conversation.LastMessageUtc ?? now,
            AssignedAgentId = assignedAgentId,
            OwnerQueueId = queueId,
        }, cancellationToken);
}
