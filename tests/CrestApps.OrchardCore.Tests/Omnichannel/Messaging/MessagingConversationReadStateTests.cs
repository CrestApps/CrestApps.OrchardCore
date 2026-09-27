using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

// Live, a workspace left open in a background tab kept polling the thread and marked every new message read before
// anyone saw it, so the Messaging > Inbox count stayed at zero.
public sealed class MessagingConversationReadStateTests
{
    [Fact]
    public void PollReads_WhenTheThreadIsNotInFrontOfTheAgent_LeavesNewMessagesUnread()
    {
        Assert.False(MessagingConversationReadState.PollReads(seen: false, newMessages: 3));
    }

    [Fact]
    public void PollReads_WhenTheAgentIsLookingAtTheThread_ReadsNewMessages()
    {
        Assert.True(MessagingConversationReadState.PollReads(seen: true, newMessages: 1));
    }

    [Fact]
    public void PollReads_WithNothingNew_ReadsNothing()
    {
        Assert.False(MessagingConversationReadState.PollReads(seen: true, newMessages: 0));
    }

    [Fact]
    public void MarkRead_OfAnUnreadConversation_ClearsItAndSaysItChanged()
    {
        var conversation = new MessagingConversation { IsRead = false, UnreadCount = 4 };

        Assert.True(MessagingConversationReadState.MarkRead(conversation));
        Assert.True(conversation.IsRead);
        Assert.Equal(0, conversation.UnreadCount);
    }

    // A transfer marks a conversation unread for its recipient without adding a message.
    [Fact]
    public void MarkRead_OfATransferredConversationWithNoNewMessages_StillReadsIt()
    {
        var conversation = new MessagingConversation { IsRead = false, UnreadCount = 0 };

        Assert.True(MessagingConversationReadState.MarkRead(conversation));
        Assert.True(conversation.IsRead);
    }

    [Fact]
    public void MarkRead_OfAReadConversation_ChangesNothing()
    {
        var conversation = new MessagingConversation { IsRead = true, UnreadCount = 0 };

        Assert.False(MessagingConversationReadState.MarkRead(conversation));
    }
}
