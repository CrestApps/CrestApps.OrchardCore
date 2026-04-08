using CrestApps.Core.AI.Services;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class SentenceBoundaryDetectorTests
{
    // --- Null / Empty / Whitespace ---

    [Fact]
    public void EndsWithSentenceBoundary_WhenNull_ShouldReturnFalse()
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(null));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenEmpty_ShouldReturnFalse()
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(""));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenWhitespaceOnly_ShouldReturnFalse()
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary("   "));
    }

    // --- Hard boundaries (. ! ? … \n) ---

    [Theory]
    [InlineData("Hello world.")]
    [InlineData("What is AI?")]
    [InlineData("This is great!")]
    [InlineData("Wait…")]
    [InlineData("First line\n")]
    public void EndsWithSentenceBoundary_WhenEndsWithHardBoundary_ShouldReturnTrue(string text)
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    [Theory]
    [InlineData("Hello world")]
    [InlineData("This is a test")]
    [InlineData("AI stands for")]
    public void EndsWithSentenceBoundary_WhenNoHardBoundary_ShouldReturnFalse(string text)
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Hard boundary with trailing whitespace ---

    [Theory]
    [InlineData("Hello world.  ")]
    [InlineData("What is AI?  ")]
    [InlineData("Great!  ")]
    public void EndsWithSentenceBoundary_WhenHardBoundaryWithTrailingWhitespace_ShouldReturnTrue(string text)
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Hard boundary with trailing wrappers (" ' ) ] }) ---

    [Theory]
    [InlineData("He said \"hello.\"")]
    [InlineData("She replied 'yes!'")]
    [InlineData("(see above.)")]
    [InlineData("[done.]")]
    [InlineData("{finished!}")]
    [InlineData("He said \"really?\"")]
    public void EndsWithSentenceBoundary_WhenHardBoundaryWithTrailingWrapper_ShouldReturnTrue(string text)
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Abbreviations (should NOT count as hard boundary) ---

    [Theory]
    [InlineData("Talk to Mr.")]
    [InlineData("Hello Mrs.")]
    [InlineData("Ask Dr.")]
    [InlineData("See Prof.")]
    [InlineData("John Sr.")]
    [InlineData("Mike Jr.")]
    [InlineData("Apples etc.")]
    [InlineData("Red vs.")]
    [InlineData("Hello Ms.")]
    public void EndsWithSentenceBoundary_WhenEndsWithAbbreviation_ShouldReturnFalse(string text)
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Abbreviations are case-insensitive ---

    [Theory]
    [InlineData("Talk to MR.")]
    [InlineData("Talk to mr.")]
    [InlineData("Ask DR.")]
    public void EndsWithSentenceBoundary_WhenAbbreviationCaseInsensitive_ShouldReturnFalse(string text)
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Soft boundaries (, ; : -) below threshold ---

    [Theory]
    [InlineData("Hello,")]
    [InlineData("First;")]
    [InlineData("Note:")]
    [InlineData("Well-")]
    public void EndsWithSentenceBoundary_WhenSoftBoundaryBelowThreshold_ShouldReturnFalse(string text)
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Soft boundaries at or above 120 chars ---

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData(':')]
    [InlineData('-')]
    public void EndsWithSentenceBoundary_WhenSoftBoundaryAtThreshold_ShouldReturnTrue(char softBoundary)
    {
        // Create a string of exactly 120 chars ending with the soft boundary.
        var text = new string('a', 119) + softBoundary;
        Assert.Equal(120, text.Length);
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    public void EndsWithSentenceBoundary_WhenSoftBoundaryBelowMinLength_ShouldReturnFalse(char softBoundary)
    {
        // Create a string of 119 chars — one below the threshold.
        var text = new string('a', 118) + softBoundary;
        Assert.Equal(119, text.Length);
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Force flush at 200 chars (any content) ---

    [Fact]
    public void EndsWithSentenceBoundary_WhenAtForceFlushLength_ShouldReturnTrue()
    {
        var text = new string('a', 200);
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenBelowForceFlushLength_ShouldReturnFalse()
    {
        var text = new string('a', 199);
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenAboveForceFlushLength_ShouldReturnTrue()
    {
        var text = new string('a', 300);
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Hard boundary followed by period that's not abbreviation ---

    [Theory]
    [InlineData("This is fine.")]
    [InlineData("I agree.")]
    [InlineData("The end.")]
    public void EndsWithSentenceBoundary_WhenPeriodNotAbbreviation_ShouldReturnTrue(string text)
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Span overload ---

    [Fact]
    public void EndsWithSentenceBoundary_SpanOverload_WhenEmpty_ShouldReturnFalse()
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void EndsWithSentenceBoundary_SpanOverload_WhenHardBoundary_ShouldReturnTrue()
    {
        var text = "Hello world.".AsSpan();
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary(text));
    }

    // --- Edge cases ---

    [Fact]
    public void EndsWithSentenceBoundary_WhenOnlyPeriod_ShouldReturnTrue()
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary("."));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenOnlyExclamation_ShouldReturnTrue()
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary("!"));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenOnlyQuestion_ShouldReturnTrue()
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary("?"));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenMultipleTrailingWrappers_ShouldReturnTrue()
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary("He said (\"yes!\")"));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenOnlyTrailingWrappers_ShouldReturnFalse()
    {
        // String of only wrappers — no boundary char behind them.
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary("\"'"));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenEllipsis_ShouldReturnTrue()
    {
        Assert.True(SentenceBoundaryDetector.EndsWithSentenceBoundary("I wonder…"));
    }

    [Fact]
    public void EndsWithSentenceBoundary_WhenNewlineInMiddle_ShouldReturnFalse()
    {
        Assert.False(SentenceBoundaryDetector.EndsWithSentenceBoundary("Hello\nWorld"));
    }
}
