using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.Tests.AI;

public sealed class RagTextNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task NormalizeContentAsync_WithNullOrWhitespace_ReturnsOriginal(string input, string expected)
    {
        Assert.Equal(expected, await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NormalizeContentAsync_StripsHtmlTags()
    {
        var input = "<p>Hello <strong>world</strong></p>";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.Contains("Hello", result);
        Assert.Contains("world", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_ConvertsBrToNewline()
    {
        var input = "Line one<br>Line two<br/>Line three<br />Line four";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<br", result);
        Assert.Contains("Line one", result);
        Assert.Contains("Line two", result);
        Assert.Contains("Line three", result);
        Assert.Contains("Line four", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_ConvertsBlockElementsToNewlines()
    {
        var input = "<h1>Title</h1><p>Paragraph one.</p><p>Paragraph two.</p>";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<h1>", result);
        Assert.DoesNotContain("<p>", result);
        Assert.Contains("Title", result);
        Assert.Contains("Paragraph one.", result);
        Assert.Contains("Paragraph two.", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_DecodesHtmlEntities()
    {
        var input = "Tom &amp; Jerry &lt;3 &gt; everyone &#x00B6;";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.Contains("Tom & Jerry", result);
        Assert.Contains("¶", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_StripsMarkdownFormatting()
    {
        var input = "# Heading\n\n**Bold text** and *italic text*\n\n- Item 1\n- Item 2";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("**", result);
        Assert.Contains("Heading", result);
        Assert.Contains("Bold text", result);
        Assert.Contains("italic text", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_HandlesMixedHtmlAndMarkdown()
    {
        var input = "# Title\n\n<p>Some <em>HTML</em> content.</p>\n\n**Markdown** text.";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.DoesNotContain("**", result);
        Assert.Contains("Title", result);
        Assert.Contains("HTML", result);
        Assert.Contains("Markdown", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_CollapsesExcessiveNewlines()
    {
        var input = "First paragraph.\n\n\n\n\nSecond paragraph.";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("\n\n\n", result);
        Assert.Contains("First paragraph.", result);
        Assert.Contains("Second paragraph.", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_HandlesEscapedHtmlFromJsonPayload()
    {
        var input = "Orchard Core \u003Ch1\u003EOrchard Core\u003Ca class=\"headerlink\" href=\"https://docs.orchardcore.net/en/latest/#orchard-core\" title=\"Permanent link\"\u003E\u00B6\u003C/a\u003E\u003C/h1\u003E";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<h1>", result);
        Assert.DoesNotContain("<a ", result);
        Assert.DoesNotContain("headerlink", result);
        Assert.Contains("Orchard Core", result);
    }

    [Fact]
    public async Task NormalizeContentAsync_PreservesPlainText()
    {
        var input = "This is plain text with no HTML or Markdown. It should remain unchanged.";
        var result = await RagTextNormalizer.NormalizeContentAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void NormalizeTitle_WithNullOrWhitespace_ReturnsOriginal(string input, string expected)
    {
        Assert.Equal(expected, RagTextNormalizer.NormalizeTitle(input));
    }

    [Fact]
    public void NormalizeTitle_StripsHtmlFromTitle()
    {
        var input = "Orchard Core <h1>Orchard Core<a class=\"headerlink\" href=\"https://docs.orchardcore.net/en/latest/#orchard-core\" title=\"Permanent link\">\u00B6</a></h1>";
        var result = RagTextNormalizer.NormalizeTitle(input);

        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.DoesNotContain("headerlink", result);
        Assert.Contains("Orchard Core", result);
    }

    [Fact]
    public void NormalizeTitle_CollapsesToSingleLine()
    {
        var input = "Line One\n\nLine Two\n\nLine Three";
        var result = RagTextNormalizer.NormalizeTitle(input);

        Assert.DoesNotContain("\n", result);
        Assert.Contains("Line One", result);
        Assert.Contains("Line Two", result);
    }

    [Fact]
    public void NormalizeTitle_TrimsWhitespace()
    {
        var input = "   Some Title   ";
        var result = RagTextNormalizer.NormalizeTitle(input);

        Assert.Equal("Some Title", result);
    }

    [Fact]
    public void NormalizeTitle_PreservesPlainTextTitle()
    {
        var input = "Simple Title";
        var result = RagTextNormalizer.NormalizeTitle(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void StripHtml_RemovesAllTags()
    {
        var input = "<div class=\"test\"><p>Text</p></div>";
        var result = RagTextNormalizer.StripHtml(input);

        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain(">", result);
        Assert.Contains("Text", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NormalizeAndChunkAsync_WithNullOrWhitespace_ReturnsEmptyList(string input)
    {
        var result = await RagTextNormalizer.NormalizeAndChunkAsync(input, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task NormalizeAndChunkAsync_ShortContent_ReturnsSingleChunk()
    {
        var input = "This is a short paragraph of text.";
        var result = await RagTextNormalizer.NormalizeAndChunkAsync(input, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Contains("short paragraph", result[0]);
    }

    [Fact]
    public async Task NormalizeAndChunkAsync_LongContent_ReturnsMultipleChunks()
    {
        var input = string.Join("\n\n", Enumerable.Range(0, 100)
            .Select(i => $"Paragraph {i}: Orchard Core is a modular application framework built on ASP.NET Core providing rich content management features."));

        var result = await RagTextNormalizer.NormalizeAndChunkAsync(input, TestContext.Current.CancellationToken);

        Assert.True(result.Count > 1, $"Expected multiple chunks but got {result.Count}.");
        Assert.All(result, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk)));
    }

    [Fact]
    public async Task NormalizeAndChunkAsync_StripsHtmlBeforeChunking()
    {
        var input = string.Join("", Enumerable.Range(0, 100)
            .Select(i => $"<p>Paragraph {i}: This is <strong>formatted</strong> content that should be cleaned up before chunking and embedding.</p>"));

        var result = await RagTextNormalizer.NormalizeAndChunkAsync(input, TestContext.Current.CancellationToken);

        Assert.NotEmpty(result);
        Assert.All(result, chunk =>
        {
            Assert.DoesNotContain("<p>", chunk);
            Assert.DoesNotContain("<strong>", chunk);
        });
    }
}
