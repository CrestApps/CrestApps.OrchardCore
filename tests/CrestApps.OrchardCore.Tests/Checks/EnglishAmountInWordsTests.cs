using CrestApps.OrchardCore.Checks.Core.Services;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class EnglishAmountInWordsTests
{
    [Theory]
    [InlineData("0", "Zero and 00/100")]
    [InlineData("0.05", "Zero and 05/100")]
    [InlineData("1", "One and 00/100")]
    [InlineData("15.5", "Fifteen and 50/100")]
    [InlineData("21", "Twenty-one and 00/100")]
    [InlineData("100", "One hundred and 00/100")]
    [InlineData("110.01", "One hundred ten and 01/100")]
    [InlineData("1234.56", "One thousand two hundred thirty-four and 56/100")]
    [InlineData("1000000", "One million and 00/100")]
    [InlineData("2005019.99", "Two million five thousand nineteen and 99/100")]
    [InlineData("999999999.995", "One billion and 00/100")]
    public void Write_WritesTheWholeAmountInWordsAndTheCentsInFigures(string amount, string expected)
    {
        // Arrange
        var writer = new EnglishAmountInWords();

        // Act
        var words = writer.Write(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        Assert.Equal(expected, words);
    }

    [Fact]
    public void Write_ACurrencyWithoutDecimals_HasNoFraction()
    {
        // Arrange
        var writer = new EnglishAmountInWords();

        // Act
        var words = writer.Write(1500m, decimals: 0);

        // Assert
        Assert.Equal("One thousand five hundred", words);
    }

    [Fact]
    public void Write_ANegativeAmount_IsRefused()
    {
        // Arrange
        var writer = new EnglishAmountInWords();

        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Write(-1m));
    }
}
