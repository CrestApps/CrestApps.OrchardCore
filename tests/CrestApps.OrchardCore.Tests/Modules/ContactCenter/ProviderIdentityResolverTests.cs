using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderIdentityResolverTests
{
    [Fact]
    public void Canonicalize_WhenNameIsAnAlias_ReturnsCanonicalName()
    {
        // Arrange
        var resolver = new ProviderIdentityResolver(
            [new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

        // Act
        var canonical = resolver.Canonicalize("Default Asterisk");

        // Assert
        Assert.Equal("Asterisk", canonical);
    }

    [Fact]
    public void Canonicalize_WhenNameIsCanonical_ReturnsSameName()
    {
        // Arrange
        var resolver = new ProviderIdentityResolver(
            [new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

        // Act
        var canonical = resolver.Canonicalize("Asterisk");

        // Assert
        Assert.Equal("Asterisk", canonical);
    }

    [Fact]
    public void Canonicalize_WhenNameIsUnknown_ReturnsSameName()
    {
        // Arrange
        var resolver = new ProviderIdentityResolver(
            [new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

        // Act
        var canonical = resolver.Canonicalize("DialPad");

        // Assert
        Assert.Equal("DialPad", canonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Canonicalize_WhenNameIsNullOrWhiteSpace_ReturnsInput(string providerName)
    {
        // Arrange
        var resolver = new ProviderIdentityResolver([]);

        // Act
        var canonical = resolver.Canonicalize(providerName);

        // Assert
        Assert.Equal(providerName, canonical);
    }

    [Fact]
    public void Canonicalize_IsCaseSensitive_ForTechnicalNames()
    {
        // Arrange
        var resolver = new ProviderIdentityResolver(
            [new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

        // Act
        var canonical = resolver.Canonicalize("default asterisk");

        // Assert
        Assert.Equal("default asterisk", canonical);
    }
}
