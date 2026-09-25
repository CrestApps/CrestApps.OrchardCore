using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// Threads written before the handoff and the inbound pipeline agreed on one writer hold two rows for each
/// message the AI handled: the live row with the provider's id, and the handoff's copy without one. The thread
/// view collapses those on read, so an existing conversation shows correctly without rewriting its data.
/// </summary>
public sealed class SmsThreadDeduplicatorTests
{
    private static readonly DateTime _at = new(2026, 9, 25, 17, 52, 14, DateTimeKind.Utc);

    [Fact]
    public void Collapse_DropsTheHandoffCopy_OfAnInboundMessageTheThreadAlreadyHolds()
    {
        // Arrange
        var live = Message("Yes ", isInbound: true, _at, providerMessageId: "SM-yes");
        var copy = Message("Yes ", isInbound: true, _at, id: "copy-1");
        var reply = Message("Great! New or used?", isInbound: false, _at.AddSeconds(19), id: "copy-2");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([live, copy, reply]);

        // Assert
        Assert.Equal([live, reply], collapsed);
    }

    [Fact]
    public void Collapse_DropsTheCopy_WhenItSortsBeforeTheLiveRow()
    {
        // Arrange
        var copy = Message("Agent please", isInbound: true, _at, id: "copy-1");
        var live = Message("Agent please", isInbound: true, _at.AddSeconds(1), providerMessageId: "SM-talk");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([copy, live]);

        // Assert
        Assert.Equal([live], collapsed);
    }

    [Fact]
    public void Collapse_KeepsTwoGenuineTexts_WithTheSameWords()
    {
        // Arrange
        // A customer who sends "ok" twice sent two messages; each carries its own provider id.
        var first = Message("ok", isInbound: true, _at, providerMessageId: "SM-1");
        var second = Message("ok", isInbound: true, _at.AddSeconds(2), providerMessageId: "SM-2");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([first, second]);

        // Assert
        Assert.Equal([first, second], collapsed);
    }

    [Fact]
    public void Collapse_KeepsAnInboundCopy_ThatHasNoLiveTwin()
    {
        // Arrange
        // Handoff copies from threads the pipeline never saw are the only record of what the customer said.
        var copy = Message("Yes", isInbound: true, _at, id: "copy-1");
        var later = Message("Yes", isInbound: true, _at.AddMinutes(10), providerMessageId: "SM-later");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([copy, later]);

        // Assert
        Assert.Equal([copy, later], collapsed);
    }

    [Fact]
    public void Collapse_FoldsNoMoreCopiesThanThereAreLiveRows()
    {
        // Arrange
        // The customer said "Yes" twice to the AI, and only one of the two was ever recorded live. One copy is a
        // duplicate of that row; the other is the only record of the second answer.
        var live = Message("Yes", isInbound: true, _at, providerMessageId: "SM-yes");
        var firstCopy = Message("Yes", isInbound: true, _at, id: "copy-1");
        var secondCopy = Message("Yes", isInbound: true, _at.AddSeconds(20), id: "copy-2");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([live, firstCopy, secondCopy]);

        // Assert
        Assert.Equal([live, secondCopy], collapsed);
    }

    [Fact]
    public void Collapse_NeverFoldsAnAutomatedReply_IntoTheCustomersText()
    {
        // Arrange
        // The AI echoing the customer's words is its own message, not a copy of theirs.
        var live = Message("Yes", isInbound: true, _at, providerMessageId: "SM-yes");
        var reply = Message("Yes", isInbound: false, _at.AddSeconds(5), id: "copy-2");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([live, reply]);

        // Assert
        Assert.Equal([live, reply], collapsed);
    }

    [Fact]
    public void Collapse_KeepsTheAgentsOwnRepeatedReply()
    {
        // Arrange
        var first = Message("Thanks!", isInbound: false, _at, id: "sent-1");
        var second = Message("Thanks!", isInbound: false, _at, id: "sent-2");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([first, second]);

        // Assert
        Assert.Equal([first, second], collapsed);
    }

    [Fact]
    public void Collapse_DropsARedeliveredRow_WithTheSameProviderMessageId()
    {
        // Arrange
        var first = Message("Is anyone there?", isInbound: true, _at, providerMessageId: "SM-retried");
        var redelivered = Message("Is anyone there?", isInbound: true, _at.AddSeconds(30), providerMessageId: "SM-retried");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([first, redelivered]);

        // Assert
        Assert.Equal([first], collapsed);
    }

    [Fact]
    public void Collapse_DropsARepeatedCopy_OfTheSameTranscriptEntry()
    {
        // Arrange
        var first = Message("Connecting you now.", isInbound: false, _at, id: "prompt-5");
        var replayed = Message("Connecting you now.", isInbound: false, _at, id: "prompt-5");

        // Act
        var collapsed = SmsThreadDeduplicator.Collapse([first, replayed]);

        // Assert
        Assert.Equal([first], collapsed);
    }

    private static OmnichannelMessage Message(string content, bool isInbound, DateTime createdUtc, string providerMessageId = null, string id = null)
        => new()
        {
            Id = id,
            Channel = "SMS",
            Content = content,
            IsInbound = isInbound,
            CreatedUtc = createdUtc,
            ConversationId = "conv-1",
            ProviderMessageId = providerMessageId,
        };
}
