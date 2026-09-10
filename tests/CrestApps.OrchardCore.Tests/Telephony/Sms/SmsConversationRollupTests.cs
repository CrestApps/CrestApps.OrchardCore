using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// The preview, unread count and last-message stamp that the inbox renders were computed in four places with
/// four slightly different truncation rules, so the same message read differently depending on which path wrote
/// it. This is the one implementation.
/// </summary>
public sealed class SmsConversationRollupTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Preview_CollapsesLineBreaks_SoAMultiLineTextDoesNotBreakTheInboxRow()
    {
        // Act
        var preview = SmsConversationRollup.BuildPreview("first line\r\nsecond line");

        // Assert
        Assert.Equal("first line second line", preview);
    }

    [Fact]
    public void Preview_TruncatesToTheInboxLength()
    {
        // Act
        var preview = SmsConversationRollup.BuildPreview(new string('a', 500));

        // Assert
        Assert.Equal(SmsConversationRollup.PreviewLength, preview.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Preview_OfNothing_IsEmpty_NotNull(string content)
    {
        // Assert
        // The inbox row binds this directly, so a null here renders as the literal word "null" in some views.
        Assert.Equal(string.Empty, SmsConversationRollup.BuildPreview(content));
    }

    [Fact]
    public void ApplyInbound_MarksTheThreadUnread_AndAdvancesTheStamps()
    {
        // Arrange
        var conversation = new SmsConversation { ItemId = "c1", IsRead = true, UnreadCount = 2 };

        // Act
        SmsConversationRollup.ApplyInbound(conversation, "a reply", _now, _now, unreadIncrement: 1);

        // Assert
        Assert.Equal("a reply", conversation.LastMessagePreview);
        Assert.Equal(_now, conversation.LastMessageUtc);
        Assert.Equal(_now, conversation.ModifiedUtc);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.False(conversation.IsRead);
    }

    [Fact]
    public void ApplyInbound_CountsEveryImportedMessage_WhenATranscriptArrivesAtOnce()
    {
        // Arrange
        // An escalation hydrates the thread with the whole automated transcript. Counting that as one unread
        // message understates what the agent has to read before replying.
        var conversation = new SmsConversation { ItemId = "c1" };

        // Act
        SmsConversationRollup.ApplyInbound(conversation, "last of many", _now, _now, unreadIncrement: 6);

        // Assert
        Assert.Equal(6, conversation.UnreadCount);
    }

    [Fact]
    public void ApplyOutbound_LeavesTheUnreadCountAlone()
    {
        // Arrange
        // The agent is the one sending, so their own message cannot make the thread more unread to them.
        var conversation = new SmsConversation { ItemId = "c1", UnreadCount = 2, IsRead = false };

        // Act
        SmsConversationRollup.ApplyOutbound(conversation, "on my way", _now, _now);

        // Assert
        Assert.Equal("on my way", conversation.LastMessagePreview);
        Assert.Equal(_now, conversation.LastMessageUtc);
        Assert.Equal(2, conversation.UnreadCount);
    }
}
