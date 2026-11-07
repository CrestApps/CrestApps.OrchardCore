using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailReplyTextTests
{
    [Fact]
    public void Split_GmailReply_SeparatesTheNewTextFromTheQuotedHistory()
    {
        // Arrange
        var body = """
            Yes, Tuesday works for me.

            Thanks,
            Ann

            On Tue, 6 Oct 2026 at 10:01, Contoso Support <support@contoso.com> wrote:
            > Would Tuesday at 10am suit you?
            >
            > Contoso
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Equal("Yes, Tuesday works for me.\n\nThanks,\nAnn", reply.Replace("\r\n", "\n"));
        Assert.StartsWith("On Tue, 6 Oct 2026", quoted);
        Assert.Contains("Would Tuesday at 10am suit you?", quoted);
    }

    [Fact]
    public void Split_GmailReplyWithAWrappedHeader_StillFindsTheQuote()
    {
        // Arrange
        var body = """
            Sounds good.

            On Tue, 6 Oct 2026 at 10:01, Contoso Support
            <support@contoso.com> wrote:
            > Would Tuesday suit you?
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Equal("Sounds good.", reply);
        Assert.Contains("Would Tuesday suit you?", quoted);
    }

    [Fact]
    public void Split_OutlookReply_CutsAtTheHeaderBlock()
    {
        // Arrange
        var body = """
            Please cancel my order.

            ________________________________
            From: Contoso Support <support@contoso.com>
            Sent: Tuesday, October 6, 2026 10:01 AM
            To: Ann Lee <ann@example.com>
            Subject: Your order 1042

            Your order has shipped.
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Equal("Please cancel my order.", reply);
        Assert.Contains("Your order has shipped.", quoted);
    }

    [Fact]
    public void Split_OriginalMessageSeparator_CutsAtTheSeparator()
    {
        // Arrange
        var body = """
            See below.

            -----Original Message-----
            From: support@contoso.com
            Earlier text.
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Equal("See below.", reply);
        Assert.StartsWith("-----Original Message-----", quoted);
    }

    [Fact]
    public void Split_InlineAnswersBetweenQuotedLines_KeepEverythingAsTheReply()
    {
        // Arrange
        // A customer who answers each question under it has written between the quotes, so the quotes are part of what
        // they said and must not be folded away.
        var body = """
            > What is your order number?
            1042
            > And your postcode?
            90210
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Contains("1042", reply);
        Assert.Contains("90210", reply);
        Assert.Empty(quoted);
    }

    [Fact]
    public void Split_AForwardWithNoNote_KeepsTheForwardedTextAsTheBody()
    {
        // Arrange
        var body = """
            ---------- Forwarded message ---------
            From: Ann <ann@example.com>
            The original text.
            """;

        // Act
        var (reply, quoted) = EmailReplyText.Split(body);

        // Assert
        Assert.Contains("The original text.", reply);
        Assert.Empty(quoted);
    }

    [Fact]
    public void Split_AMessageWithNoQuote_IsAllReply()
    {
        // Act
        var (reply, quoted) = EmailReplyText.Split("Hello,\n\nI have a question about my bill.");

        // Assert
        Assert.Equal("Hello,\n\nI have a question about my bill.", reply);
        Assert.Empty(quoted);
    }
}
