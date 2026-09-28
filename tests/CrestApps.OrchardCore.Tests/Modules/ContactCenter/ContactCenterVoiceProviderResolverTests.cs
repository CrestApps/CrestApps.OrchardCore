using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterVoiceProviderResolverTests
{
    [Fact]
    public void Get_WithExplicitTechnicalName_ReturnsThatProvider()
    {
        // Arrange
        var telnyx = CreateProvider("Telnyx");
        var dialpad = CreateProvider("Dialpad");
        var resolver = CreateResolver([telnyx, dialpad], defaultProviderName: "Telnyx");

        // Act
        var result = resolver.Get("Dialpad");

        // Assert: an explicitly named provider is resolved by technical name regardless of the default.
        Assert.Same(dialpad, result);
    }

    [Fact]
    public void Get_WithoutName_WhenMultipleProvidersRegistered_ReturnsTheConfiguredDefault()
    {
        // Arrange: three provider adapters register at once (all three modules enabled alongside Contact Center
        // Voice). The configured default is the second-registered one, so a naive first-registered fallback
        // would return the wrong provider.
        var telnyx = CreateProvider("Telnyx");
        var dialpad = CreateProvider("Dialpad");
        var asterisk = CreateProvider("Asterisk");
        var resolver = CreateResolver([telnyx, dialpad, asterisk], defaultProviderName: "Dialpad");

        // Act
        var result = resolver.Get();

        // Assert
        Assert.Same(dialpad, result);
    }

    [Fact]
    public void Get_WithoutName_WhenConfiguredDefaultIsNotRegistered_FallsBackToFirst()
    {
        // Arrange: the default names a provider whose voice adapter is not registered (its module is not enabled
        // for Contact Center voice), so the resolver falls back to the first registered provider.
        var dialpad = CreateProvider("Dialpad");
        var asterisk = CreateProvider("Asterisk");
        var resolver = CreateResolver([dialpad, asterisk], defaultProviderName: "Telnyx");

        // Act
        var result = resolver.Get();

        // Assert
        Assert.Same(dialpad, result);
    }

    [Fact]
    public void Get_WithoutName_WhenNoDefaultConfigured_FallsBackToFirst()
    {
        // Arrange
        var dialpad = CreateProvider("Dialpad");
        var asterisk = CreateProvider("Asterisk");
        var resolver = CreateResolver([dialpad, asterisk], defaultProviderName: null);

        // Act
        var result = resolver.Get();

        // Assert
        Assert.Same(dialpad, result);
    }

    private static ContactCenterVoiceProviderResolver CreateResolver(
        IEnumerable<IContactCenterVoiceProvider> providers,
        string defaultProviderName)
    {
        var telephonySettings = new Mock<IOptionsSnapshot<TelephonySettings>>();
        telephonySettings
            .SetupGet(options => options.Value)
            .Returns(new TelephonySettings { DefaultProviderName = defaultProviderName });

        return new ContactCenterVoiceProviderResolver(providers, telephonySettings.Object);
    }

    private static IContactCenterVoiceProvider CreateProvider(string technicalName)
    {
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(item => item.TechnicalName).Returns(technicalName);

        return provider.Object;
    }
}
