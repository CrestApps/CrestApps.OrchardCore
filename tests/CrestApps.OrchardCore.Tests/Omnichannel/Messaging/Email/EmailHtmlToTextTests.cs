using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailHtmlToTextTests
{
    [Fact]
    public void Convert_KeepsParagraphsAndLineBreaks()
    {
        // Act
        var text = EmailHtmlToText.Convert("<p>Hello Ann,</p><p>Line one<br>Line two</p>");

        // Assert
        Assert.Equal("Hello Ann,\n\nLine one\nLine two", text);
    }

    [Fact]
    public void Convert_DropsHeadScriptsAndStyles()
    {
        // Act
        var text = EmailHtmlToText.Convert("<html><head><title>x</title><style>p{color:red}</style></head><body><script>alert(1)</script><p>Visible</p></body></html>");

        // Assert
        Assert.Equal("Visible", text);
    }

    [Fact]
    public void Convert_DecodesEntities()
    {
        // Act
        var text = EmailHtmlToText.Convert("<p>Fish &amp; chips &lt;3&nbsp;today</p>");

        // Assert
        Assert.Equal("Fish & chips <3 today", text);
    }

    [Fact]
    public void Convert_KeepsALinkAddressWhenTheTextIsNotTheAddress()
    {
        // Act
        var text = EmailHtmlToText.Convert("<p>See <a href=\"https://contoso.com/track/1042\">your order</a>.</p>");

        // Assert
        Assert.Equal("See your order (https://contoso.com/track/1042).", text);
    }

    [Fact]
    public void Convert_MarksABlockquoteAsQuotedSoTheReplyParserFoldsIt()
    {
        // Arrange
        var html = "<div>Thanks, that works.</div><div class=\"gmail_quote\"><div>On Tue, Contoso wrote:</div><blockquote><p>Does Tuesday suit you?</p></blockquote></div>";

        // Act
        var text = EmailHtmlToText.Convert(html);
        var (reply, quoted) = EmailReplyText.Split(text);

        // Assert
        Assert.Equal("Thanks, that works.", reply);
        Assert.Contains("Does Tuesday suit you?", quoted);
    }

    [Fact]
    public void Convert_RendersListItems()
    {
        // Act
        var text = EmailHtmlToText.Convert("<ul><li>One</li><li>Two</li></ul>");

        // Assert
        Assert.Contains("• One", text);
        Assert.Contains("• Two", text);
    }
}
