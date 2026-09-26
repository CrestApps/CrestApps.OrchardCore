using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingConversationTransferService"/>. Whoever holds a conversation, or a supervisor, can
/// hand it to another person or send it back to a team's shared pool; the conversation keeps its identity and so
/// every message it carries.
/// </summary>
public sealed class MessagingConversationTransferService : IMessagingConversationTransferService
{
    /// <summary>
    /// The longest note a transfer keeps. A longer one is cut, since it is a hand-over line, not a message.
    /// </summary>
    public const int MaxNoteLength = 500;

    /// <summary>
    /// The most history entries a conversation keeps; the oldest are dropped first.
    /// </summary>
    public const int MaxHistoryEntries = 50;

    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingConversationAuthorizationService _conversationAuthorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;
    private readonly IMessagingAgentNameProvider _agentNames;
    private readonly IMessagingRealTimeNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationTransferService"/> class.
    /// </summary>
    public MessagingConversationTransferService(
        IMessagingConversationStore conversationStore,
        IMessagingConversationAuthorizationService conversationAuthorizationService,
        IAgentProfileManager agentProfileManager,
        IEnumerable<IActivityQueueManager> queueManagers,
        IAgentEntitlementPolicy entitlementPolicy,
        IMessagingAgentNameProvider agentNames,
        IMessagingRealTimeNotifier notifier,
        IClock clock,
        ILogger<MessagingConversationTransferService> logger)
    {
        _conversationStore = conversationStore;
        _conversationAuthorizationService = conversationAuthorizationService;
        _agentProfileManager = agentProfileManager;
        // Queues belong to a feature the workspace does not require. Without it a conversation can still go to a
        // person, just not back to a team.
        _queueManager = queueManagers.FirstOrDefault();
        _entitlementPolicy = entitlementPolicy;
        _agentNames = agentNames;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingTransferResult> TransferAsync(MessagingTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetId))
        {
            return MessagingTransferResult.Failed(request.TargetType == ConversationRouteTargetType.Queue
                ? "Choose the team to send the conversation to."
                : "Choose the person to transfer the conversation to.");
        }

        var conversation = await _conversationStore.FindByIdAsync(request.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return MessagingTransferResult.Failed("The conversation was not found.");
        }

        if (request.Principal is not null &&
            !await _conversationAuthorizationService.AuthorizeAsync(request.Principal, conversation, ConversationOperation.Transfer, cancellationToken))
        {
            return MessagingTransferResult.Failed("You are not allowed to transfer this conversation.");
        }

        var previousAgentId = conversation.GetHolderAgentId();
        var previousQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null;
        var targetId = request.TargetId.Trim();

        string targetName;

        if (request.TargetType == ConversationRouteTargetType.Queue)
        {
            var queue = _queueManager is null ? null : await _queueManager.FindByIdAsync(targetId, cancellationToken);

            if (queue is null || !queue.Enabled || ContactCenterConstants.IsDirectRoutingQueue(queue.ItemId))
            {
                return MessagingTransferResult.Failed("The team was not found.");
            }

            if (previousAgentId is null && string.Equals(previousQueueId, queue.ItemId, StringComparison.Ordinal))
            {
                return MessagingTransferResult.Failed("The conversation is already waiting in that team's shared inbox.");
            }

            // Back in the team's pool: nobody holds it, and every member may claim it.
            conversation.OwnerType = ConversationOwnerType.Queue;
            conversation.OwnerId = queue.ItemId;
            conversation.AssignedAgentId = null;
            conversation.AssignmentStatus = ConversationAssignmentStatus.Pooled;
            targetName = string.IsNullOrWhiteSpace(queue.Name) ? null : queue.Name;
        }
        else
        {
            var target = await _agentProfileManager.FindByIdAsync(targetId, cancellationToken);

            if (target is null)
            {
                return MessagingTransferResult.Failed("The person was not found.");
            }

            if (string.Equals(previousAgentId, target.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                return MessagingTransferResult.Failed("The conversation is already with that person.");
            }

            // A queue conversation stays with its team when the recipient serves that team, so it still counts for the
            // team. Assigned to someone who does not, they could never open it, so it becomes their own instead.
            if (conversation.OwnerType != ConversationOwnerType.Queue || !ServesQueue(target, conversation.OwnerId))
            {
                conversation.OwnerType = ConversationOwnerType.Personal;
                conversation.OwnerId = target.ItemId;
            }

            conversation.AssignedAgentId = target.ItemId;
            conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;
            targetName = await _agentNames.GetDisplayNameAsync(target.ItemId, cancellationToken);
        }

        // A person picked where it goes, so the routed pickup clock no longer applies, and the recipient has not read
        // it yet.
        conversation.AssignedUtc = null;
        conversation.ReassignmentAttempts = 0;
        conversation.IsRead = false;
        conversation.ModifiedUtc = _clock.UtcNow;

        var entry = new MessagingConversationEvent
        {
            Kind = MessagingConversationEventKind.Transferred,
            OccurredUtc = _clock.UtcNow,
            ActorAgentId = request.ActingAgentId,
            ActorName = string.IsNullOrEmpty(request.ActingAgentId) ? null : await _agentNames.GetDisplayNameAsync(request.ActingAgentId, cancellationToken),
            FromAgentId = previousAgentId,
            FromName = previousAgentId is not null
                ? await _agentNames.GetDisplayNameAsync(previousAgentId, cancellationToken)
                : await GetQueueNameAsync(previousQueueId, cancellationToken),
            ToAgentId = request.TargetType == ConversationRouteTargetType.Queue ? null : conversation.AssignedAgentId,
            ToQueueId = request.TargetType == ConversationRouteTargetType.Queue ? conversation.OwnerId : null,
            ToName = targetName,
            Note = NormalizeNote(request.Note),
        };

        conversation.History ??= [];
        conversation.History.Add(entry);

        while (conversation.History.Count > MaxHistoryEntries)
        {
            conversation.History.RemoveAt(0);
        }

        await _conversationStore.UpdateAsync(conversation, cancellationToken);

        await _notifier.ConversationAssignedAsync(new MessagingAssignmentNotification
        {
            ConversationId = conversation.ItemId,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
            IsTransfer = true,
            PreviousAgentId = previousAgentId,
            TransferredByAgentId = request.ActingAgentId,
            TransferredByName = entry.ActorName,
            TransferredToName = entry.ToName,
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Messaging conversation {ConversationId} was transferred from agent {FromAgentId} to {TargetType} {TargetId} by agent {ActorAgentId}.",
                conversation.ItemId.SanitizeLogValue(),
                previousAgentId.SanitizeLogValue(),
                request.TargetType,
                targetId.SanitizeLogValue(),
                request.ActingAgentId.SanitizeLogValue());
        }

        return new MessagingTransferResult
        {
            Succeeded = true,
            Conversation = conversation,
            Event = entry,
        };
    }

    // The same membership rule the conversation authorization applies, so a recipient kept on the team can open it.
    private bool ServesQueue(AgentProfile agent, string queueId)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return false;
        }

        var belongs = agent.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true ||
            agent.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true;

        return belongs && _entitlementPolicy.AllowsQueue(agent, queueId);
    }

    private async Task<string> GetQueueNameAsync(string queueId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueId) || _queueManager is null)
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        return string.IsNullOrWhiteSpace(queue?.Name) ? null : queue.Name;
    }

    private static string NormalizeNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();

        return trimmed.Length > MaxNoteLength ? trimmed[..MaxNoteLength].TrimEnd() : trimmed;
    }
}
