using CrestApps.OrchardCore.Omnichannel.Core;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class OmnichannelEmailAddressTests
{
    [Theory]
    [InlineData("ann@example.com", "ann@example.com")]
    [InlineData("  Ann@Example.COM ", "ann@example.com")]
    [InlineData("mailto:Ann@Example.com", "ann@example.com")]
    [InlineData("mailto:ann@example.com?subject=Hello", "ann@example.com")]
    [InlineData("\"Ann Lee\" <Ann@Example.com>", "ann@example.com")]
    [InlineData("Ann Lee <ann@example.com>", "ann@example.com")]
    [InlineData("<ann@example.com>", "ann@example.com")]
    [InlineData("ann+orders@example.com", "ann+orders@example.com")]
    public void Normalize_EverySpellingOfOneAddress_MeetsInTheSameCanonicalForm(string address, string expected)
    {
        // Act
        var normalized = OmnichannelEmailAddress.Normalize(address);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<>")]
    public void Normalize_WhenTheAddressIsEmpty_ReturnsNull(string address)
    {
        // Act & Assert
        Assert.Null(OmnichannelEmailAddress.Normalize(address));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        // Arrange
        var once = OmnichannelEmailAddress.Normalize("\"Ann\" <Ann@Example.com>");

        // Act
        var twice = OmnichannelEmailAddress.Normalize(once);

        // Assert
        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("ann@example.com", true)]
    [InlineData("Ann Lee <ann@example.co.uk>", true)]
    [InlineData("ann@localhost", false)]
    [InlineData("ann.example.com", false)]
    [InlineData("ann@@example.com", false)]
    [InlineData("@example.com", false)]
    [InlineData("ann@", false)]
    [InlineData("ann@example..com", false)]
    [InlineData("ann smith@example.com", false)]
    [InlineData("", false)]
    public void IsValid_AcceptsAddressesAndRefusesEverythingElse(string address, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, OmnichannelEmailAddress.IsValid(address));
    }
}
