using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailHtmlToTextTests
{
    [Fact]
    public void Convert_MarkupBuiltToMakeThePatternsBacktrack_StillGivesTheText()
    {
        // Arrange: every unclosed script makes the pattern for invisible elements search the rest of the body again.
        var html = "<p>Hello</p>" + string.Concat(Enumerable.Repeat("<script>", 100_000));

        // Act
        var text = EmailHtmlToText.Convert(html);

        // Assert: the conversion gives up on the patterns after their time limit instead of running for hours.
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void Convert_OnlyConvertsTheStartOfAHugeBody()
    {
        // Arrange
        var html = "<p>Start</p><p>" + new string('x', EmailHtmlToText.MaxHtmlLength) + "</p><p>The end</p>";

        // Act
        var text = EmailHtmlToText.Convert(html);

        // Assert
        Assert.StartsWith("Start", text);
        Assert.DoesNotContain("The end", text);
        Assert.True(text.Length <= EmailHtmlToText.MaxHtmlLength);
    }

    [Fact]
    public void StripTags_DropsTheMarkupScriptsAndCommentsAndKeepsTheBlocksApart()
    {
        // Act
        var text = EmailHtmlToText.StripTags("<html><head><title>x</title><style>p{color:red}</style></head><body><!-- note --><p>Fish &amp; <b>chips</b></p><div>Line&nbsp;two</div><script>alert(1)</script></body></html>");

        // Assert
        Assert.Equal("Fish & chips\n\nLine two", text);
    }

    [Fact]
    public void StripTags_AnUnclosedScriptHidesTheRest()
    {
        // Act
        var text = EmailHtmlToText.StripTags("<p>Visible</p><script>" + string.Concat(Enumerable.Repeat("<script>", 1_000)));

        // Assert
        Assert.Equal("Visible", text);
    }

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
