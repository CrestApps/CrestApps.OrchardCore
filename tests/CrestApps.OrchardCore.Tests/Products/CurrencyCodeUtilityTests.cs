using CrestApps.OrchardCore.Products.Core;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class CurrencyCodeUtilityTests
{
    [Theory]
    [InlineData("EUR", true)]
    [InlineData(" usd ", true)]
    [InlineData("US", false)]
    [InlineData("US1", false)]
    [InlineData("ÄÖÜ", false)]
    [InlineData(null, false)]
    public void IsValid_AcceptsOnlyThreeAsciiLetters(string currencyCode, bool expected)
        => Assert.Equal(expected, CurrencyCodeUtility.IsValid(currencyCode));

    [Fact]
    public void Normalize_TrimsAndUppercases()
        => Assert.Equal("USD", CurrencyCodeUtility.Normalize(" usd "));
}
