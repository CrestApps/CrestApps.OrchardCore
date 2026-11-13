using System.Globalization;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Tests.Core.Currencies;

public sealed class CurrencyFormatTests
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr-FR");

    [Theory]
    [InlineData(20, "EUR", "EUR 20.00")]
    [InlineData(1234.5, "USD", "USD 1234.50")]
    [InlineData(1500, "JPY", "JPY 1500")]
    [InlineData(1.2345, "KWD", "KWD 1.235")]
    [InlineData(20, null, "20.00")]
    [InlineData(20, "", "20.00")]
    public void WithCode_WritesTheCodeAndTheAmountAtTheCurrencyPrecision(double amount, string currency, string expected)
    {
        // Act
        var text = CurrencyFormat.WithCode((decimal)amount, currency);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void WithSymbol_UsesTheSymbolOfTheCurrency_NotOfTheCulture()
    {
        // Act
        var dollars = CurrencyFormat.WithSymbol(1234.5m, "USD", _english);
        var euros = CurrencyFormat.WithSymbol(1234.5m, "EUR", _english);

        // Assert
        Assert.Equal("$1,234.50", dollars);
        Assert.Equal("€1,234.50", euros);
    }

    [Fact]
    public void WithSymbol_UsesTheNumberFormatOfTheCulture()
    {
        // Act
        var text = CurrencyFormat.WithSymbol(1234.5m, "EUR", _french);

        // Assert: the culture puts the symbol after the amount and uses its own decimal separator.
        Assert.EndsWith("€", text);
        Assert.Contains("234" + _french.NumberFormat.CurrencyDecimalSeparator + "50", text);
    }

    [Fact]
    public void WithSymbol_UsesThePrecisionOfTheCurrency()
    {
        // Act
        var yen = CurrencyFormat.WithSymbol(1500m, "JPY", _english);
        var dinars = CurrencyFormat.WithSymbol(1.5m, "KWD", _english);

        // Assert
        Assert.DoesNotContain(".", yen);
        Assert.EndsWith("1.500", dinars);
    }

    [Fact]
    public void WithSymbol_TrimsAWholeAmount_OnlyWhenAsked()
    {
        // Act
        var trimmed = CurrencyFormat.WithSymbol(15599m, "USD", _english, trimWholeAmounts: true);
        var withCents = CurrencyFormat.WithSymbol(67.46m, "USD", _english, trimWholeAmounts: true);
        var full = CurrencyFormat.WithSymbol(15599m, "USD", _english);

        // Assert
        Assert.Equal("$15,599", trimmed);
        Assert.Equal("$67.46", withCents);
        Assert.Equal("$15,599.00", full);
    }

    [Fact]
    public void GetSymbol_ForACurrencyNoCultureUses_IsItsCode()
    {
        // Act
        var symbol = CurrencyFormat.GetSymbol("xts");

        // Assert
        Assert.Equal("XTS ", symbol);
    }

    [Fact]
    public void DefaultMoneyFormatter_WritesTheSymbolInTheReadersCulture()
    {
        // Arrange
        var formatter = new DefaultMoneyFormatter();

        // Act
        var text = formatter.Format(1250m, "USD", _english);

        // Assert
        Assert.Equal("$1,250.00", text);
    }
}
