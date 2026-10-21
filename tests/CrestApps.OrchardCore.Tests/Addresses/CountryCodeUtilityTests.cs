using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// A country code is two letters, stored in capitals, and a Country content item keeps it in its part's code field.
/// </summary>
public sealed class CountryCodeUtilityTests
{
    [Theory]
    [InlineData("US", true)]
    [InlineData(" de ", true)]
    [InlineData("USA", false)]
    [InlineData("U1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ACountryCode_IsTwoLetters(string code, bool expected)
    {
        Assert.Equal(expected, CountryCodeUtility.IsCountryCode(code));
    }

    [Fact]
    public void ACountryCode_IsTrimmedAndInCapitals()
    {
        Assert.Equal("DE", CountryCodeUtility.Normalize(" de "));
        Assert.Null(CountryCodeUtility.Normalize("  "));
    }

    [Fact]
    public void ACountryItem_KeepsItsCodeInItsPartsCodeField()
    {
        // Arrange
        var country = new ContentItem { ContentType = AddressConstants.Country };

        // Act
        var before = country.GetCountryCode();
        country.SetCountryCode("CA");

        // Assert
        Assert.Null(before);
        Assert.Equal("CA", country.GetCountryCode());
        Assert.Equal("CA", (string)country.Content.CountryPart.Code.Text);
    }
}
