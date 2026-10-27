using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Builds the <see cref="MessagingAssignmentNotification"/> every assignment path raises, so a claim, a transfer and a
/// routed placement all tell the new holder the same things: who the conversation is with and who put it there.
/// </summary>
internal static class MessagingAssignmentNotifications
{
    /// <summary>
    /// Creates the notification for a conversation's current assignment.
    /// </summary>
    /// <param name="conversation">The conversation, after the assignment was applied.</param>
    /// <param name="previousAgentId">The agent who held it before, whose inbox drops it.</param>
    /// <param name="assignedByAgentId">The agent who made the assignment, or <see langword="null"/> for routing.</param>
    public static MessagingAssignmentNotification Create(MessagingConversation conversation, string previousAgentId, string assignedByAgentId)
        => new()
        {
            ConversationId = conversation.ItemId,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
            PreviousAgentId = previousAgentId,
            AssignedByAgentId = assignedByAgentId,
            Channel = conversation.Channel,
            ContactAddress = conversation.ContactAddress,
            Preview = conversation.LastMessagePreview,
        };

    /// <summary>
    /// Tells the agent routing placed a conversation with, and the agent it was taken from, when routing changed who
    /// holds it. Nothing is sent when the holder did not change.
    /// </summary>
    /// <param name="notifier">The real-time notifier.</param>
    /// <param name="conversation">The conversation, after it was routed and saved.</param>
    /// <param name="previousAgentId">The agent who held it before routing ran.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task NotifyRoutedAsync(IMessagingRealTimeNotifier notifier, MessagingConversation conversation, string previousAgentId, CancellationToken cancellationToken)
    {
        var assignedAgentId = string.IsNullOrEmpty(conversation.AssignedAgentId) ? null : conversation.AssignedAgentId;
        previousAgentId = string.IsNullOrEmpty(previousAgentId) ? null : previousAgentId;

        if (string.Equals(assignedAgentId, previousAgentId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var notification = Create(conversation, previousAgentId, assignedByAgentId: null);

        // Only the two agents are told. The queue's other members never saw a thread pushed past them, and a thread
        // returned to the pool already lights the queue's inbox through the inbound notification sent alongside.
        notification.OwnerQueueId = null;

        return notifier.ConversationAssignedAsync(notification, cancellationToken);
    }
}
