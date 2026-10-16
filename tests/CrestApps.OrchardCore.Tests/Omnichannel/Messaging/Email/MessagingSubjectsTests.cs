using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class MessagingSubjectsTests
{
    [Theory]
    [InlineData("Order 1042", "Re: Order 1042")]
    [InlineData("Re: Order 1042", "Re: Order 1042")]
    [InlineData("RE: Order 1042", "RE: Order 1042")]
    [InlineData("AW: Bestellung", "AW: Bestellung")]
    [InlineData("  Order 1042  ", "Re: Order 1042")]
    public void ForReply_AddsOneReplyPrefix(string subject, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, MessagingSubjects.ForReply(subject));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ForReply_WhenThereIsNoSubject_ReturnsNull(string subject)
    {
        // Act & Assert
        Assert.Null(MessagingSubjects.ForReply(subject));
    }

    [Fact]
    public void Clean_FoldsLineBreaksSoASubjectCannotInjectAHeader()
    {
        // Act
        var cleaned = MessagingSubjects.Clean("Hello\r\nBcc: someone@example.com");

        // Assert
        Assert.Equal("Hello Bcc: someone@example.com", cleaned);
        Assert.DoesNotContain('\n', cleaned);
        Assert.DoesNotContain('\r', cleaned);
    }

    [Fact]
    public void Clean_BoundsTheLength()
    {
        // Act
        var cleaned = MessagingSubjects.Clean(new string('a', 400));

        // Assert
        Assert.Equal(MessagingSubjects.MaxLength, cleaned.Length);
    }

    [Fact]
    public void ForReplyTo_UsesTheNewestMessageThatHasASubject()
    {
        // Arrange
        var older = new OmnichannelMessage { CreatedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) };
        older.SetSubject("First question");

        var newer = new OmnichannelMessage { CreatedUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc) };
        newer.SetSubject("Second question");

        var newestWithoutSubject = new OmnichannelMessage { CreatedUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc) };

        // Act
        var subject = MessagingSubjects.ForReplyTo([older, newestWithoutSubject, newer]);

        // Assert
        Assert.Equal("Re: Second question", subject);
    }

    [Fact]
    public void SetSubject_KeepsTheSubjectOnThePropertyBagNotOnTheMessage()
    {
        // Arrange
        var message = new OmnichannelMessage();

        // Act
        message.SetSubject("  Hello  ");

        // Assert
        Assert.Equal("Hello", message.GetSubject());
        Assert.True(message.Properties.ContainsKey(nameof(MessagingMessageDetails)));
    }
}
