using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

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
    private readonly ISmsConversationRouter _router;
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
        ISmsConversationRouter router,
        ISmsRealTimeNotifier notifier,
        IClock clock,
        IOptions<SmsRoutedDistributionOptions> options,
        ILogger<SmsRoutedReassignmentService> logger)
    {
        _conversationStore = conversationStore;
        _router = router;
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

            // The placement decision belongs to the router, told that this pass is a reassignment. This service
            // owns when a thread is stale, persisting the outcome, and re-lighting the right inbox.
            await _router.RouteAsync(
                new SmsRoutingContext
                {
                    Trigger = SmsRoutingTrigger.Reassignment,
                    Conversation = conversation,
                    ExcludeAgentId = conversation.AssignedAgentId,
                    MaxReassignmentAttempts = _maxReassignmentAttempts,
                },
                cancellationToken);

            await _conversationStore.UpdateAsync(conversation, cancellationToken);

            // Light the inbox the thread just landed in: the newly-assigned agent, or the queue when it was
            // returned to the shared pool.
            await NotifyAsync(conversation, conversation.AssignedAgentId, queueId, now, cancellationToken);

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
