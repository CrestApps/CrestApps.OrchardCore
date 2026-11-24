using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Tests.Core.Numbering;

public sealed class NumberSeriesFormatTests
{
    private static readonly DateOnly _date = new(2026, 3, 9);

    [Theory]
    [InlineData("{seq}", 42, "42")]
    [InlineData("INV-{seq}", 1001, "INV-1001")]
    [InlineData("INV-{seq:0000}", 42, "INV-0042")]
    [InlineData("INV-{seq:0000}", 123456, "INV-123456")]
    [InlineData("{yyyy}/{seq:000}", 7, "2026/007")]
    [InlineData("CN{yy}{MM}-{seq:00}", 7, "CN2603-07")]
    public void Format_WritesTheNumberWithItsTokens(string pattern, long sequence, string expected)
    {
        // Arrange
        var format = new NumberSeriesFormat(pattern);

        // Act
        var text = format.Format(sequence, _date);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Format_WithAPatternThatCannotBeRead_WritesTheBareNumber()
    {
        // Arrange
        var format = new NumberSeriesFormat("INV-{oops}");

        // Act
        var text = format.Format(42, _date);

        // Assert
        Assert.Equal("42", text);
    }

    [Theory]
    [InlineData("INV-{seq:0000}", NumberSeriesReset.Never, NumberSeriesFormatError.None)]
    [InlineData("{yyyy}-{seq}", NumberSeriesReset.Yearly, NumberSeriesFormatError.None)]
    [InlineData("{yy}{MM}-{seq}", NumberSeriesReset.Monthly, NumberSeriesFormatError.None)]
    [InlineData("", NumberSeriesReset.Never, NumberSeriesFormatError.Empty)]
    [InlineData("INV", NumberSeriesReset.Never, NumberSeriesFormatError.MissingSequence)]
    [InlineData("{seq}-{seq}", NumberSeriesReset.Never, NumberSeriesFormatError.SeveralSequences)]
    [InlineData("{seq:12}", NumberSeriesReset.Never, NumberSeriesFormatError.UnknownToken)]
    [InlineData("{seq:}", NumberSeriesReset.Never, NumberSeriesFormatError.UnknownToken)]
    [InlineData("{dd}-{seq}", NumberSeriesReset.Never, NumberSeriesFormatError.UnknownToken)]
    [InlineData("INV-{seq", NumberSeriesReset.Never, NumberSeriesFormatError.UnknownToken)]
    [InlineData("INV}-{seq}", NumberSeriesReset.Never, NumberSeriesFormatError.UnknownToken)]
    [InlineData("INV-{seq}", NumberSeriesReset.Yearly, NumberSeriesFormatError.ResetNeedsYear)]
    [InlineData("{yyyy}-{seq}", NumberSeriesReset.Monthly, NumberSeriesFormatError.ResetNeedsMonth)]
    public void Validate_SaysWhyAFormatCannotBeUsed(string pattern, NumberSeriesReset reset, NumberSeriesFormatError expected)
    {
        // Arrange
        var format = new NumberSeriesFormat(pattern, reset: reset);

        // Act
        var error = format.Validate();

        // Assert
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Validate_RefusesAPatternLongerThanTheMaximum()
    {
        // Arrange
        var format = new NumberSeriesFormat(new string('A', NumberSeriesFormat.MaximumPatternLength) + "{seq}");

        // Act
        var error = format.Validate();

        // Assert
        Assert.Equal(NumberSeriesFormatError.TooLong, error);
    }

    [Fact]
    public void Validate_RefusesAFirstNumberBelowOne()
    {
        // Arrange
        var format = new NumberSeriesFormat("{seq}", firstNumber: 0);

        // Act
        var error = format.Validate();

        // Assert
        Assert.Equal(NumberSeriesFormatError.FirstNumberTooLow, error);
    }

    [Theory]
    [InlineData(NumberSeriesReset.Never, null)]
    [InlineData(NumberSeriesReset.Yearly, "2026")]
    [InlineData(NumberSeriesReset.Monthly, "2026-03")]
    public void GetPeriod_IsTheYearOrMonthTheSeriesCountsIn(NumberSeriesReset reset, string expected)
    {
        // Arrange
        var format = new NumberSeriesFormat("{yyyy}{MM}-{seq}", reset: reset);

        // Act
        var period = format.GetPeriod(_date);

        // Assert
        Assert.Equal(expected, period);
    }
}
