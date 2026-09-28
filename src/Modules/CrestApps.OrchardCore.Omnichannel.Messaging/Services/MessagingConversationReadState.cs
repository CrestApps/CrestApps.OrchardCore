using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Decides when the workspace marks a conversation read. The unread flag is the conversation's, shared by everyone who
/// can see it, and it is what the Messaging > Inbox count is made of, so it may only be cleared by somebody actually
/// looking at the thread.
/// </summary>
public static class MessagingConversationReadState
{
    /// <summary>
    /// Determines whether a poll of the open thread reads it. A workspace left open in a background tab keeps polling;
    /// reading for it cleared every new message before anyone saw it, and the menu count stayed at zero.
    /// </summary>
    /// <param name="seen">Whether the page said the thread is in front of the agent.</param>
    /// <param name="newMessages">How many messages the poll brought.</param>
    /// <returns><see langword="true"/> when the conversation should be marked read.</returns>
    public static bool PollReads(bool seen, int newMessages)
        => seen && newMessages > 0;

    /// <summary>
    /// Marks the conversation read.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <returns><see langword="true"/> when it was unread, so the change has to be saved.</returns>
    public static bool MarkRead(MessagingConversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (conversation.IsRead && conversation.UnreadCount == 0)
        {
            return false;
        }

        conversation.IsRead = true;
        conversation.UnreadCount = 0;

        return true;
    }
}
