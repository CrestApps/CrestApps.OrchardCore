using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterVoiceMediaProviderResolverTests
{
    [Fact]
    public void Get_WhenMatchingMediaContractIsRegistered_ReturnsMediaProvider()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "asterisk",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var mediaProvider = CreateMediaProvider("asterisk");
        var voiceProviderResolver = CreateVoiceProviderResolver(voiceProvider);
        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            [mediaProvider.Object]);

        // Act
        var result = resolver.Get("asterisk");

        // Assert
        Assert.Same(mediaProvider.Object, result);
    }

    [Fact]
    public void Get_WhenMatchingMediaContractIsRegistered_DoesNotRequireBaseProviderCapability()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "dialpad",
            ContactCenterVoiceProviderCapabilities.None);
        var mediaProvider = CreateMediaProvider("dialpad");
        var voiceProviderResolver = CreateVoiceProviderResolver(voiceProvider);
        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            [mediaProvider.Object]);

        // Act
        var result = resolver.Get("dialpad");

        // Assert
        Assert.Same(mediaProvider.Object, result);
    }

    [Fact]
    public void Get_WhenMediaContractIsNotRegistered_ReturnsNull()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "provider",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var voiceProviderResolver = CreateVoiceProviderResolver(voiceProvider);
        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            []);

        // Act
        var result = resolver.Get("provider");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAll_ReturnsOnlyMediaContractsWithRegisteredVoiceProviderIdentity()
    {
        // Arrange
        var firstVoiceProvider = CreateVoiceProvider(
            "asterisk",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var secondVoiceProvider = CreateVoiceProvider(
            "dialpad",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var firstMediaProvider = CreateMediaProvider("asterisk");
        var secondMediaProvider = CreateMediaProvider("dialpad");
        var orphanedMediaProvider = CreateMediaProvider("unregistered");
        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();

        voiceProviderResolver
            .Setup(resolver => resolver.GetAll())
            .Returns([firstVoiceProvider.Object, secondVoiceProvider.Object]);

        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            [firstMediaProvider.Object, secondMediaProvider.Object, orphanedMediaProvider.Object]);

        // Act
        var results = resolver.GetAll();

        // Assert
        Assert.Collection(
            results,
            provider => Assert.Same(firstMediaProvider.Object, provider),
            provider => Assert.Same(secondMediaProvider.Object, provider));
    }

    private static Mock<IContactCenterVoiceProviderResolver> CreateVoiceProviderResolver(
        Mock<IContactCenterVoiceProvider> voiceProvider)
    {
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();

        resolver
            .Setup(providerResolver => providerResolver.Get(It.IsAny<string>()))
            .Returns(voiceProvider.Object);

        return resolver;
    }

    private static Mock<IContactCenterVoiceProvider> CreateVoiceProvider(
        string technicalName,
        ContactCenterVoiceProviderCapabilities capabilities)
    {
        var provider = new Mock<IContactCenterVoiceProvider>();

        provider.SetupGet(item => item.TechnicalName).Returns(technicalName);
        provider.SetupGet(item => item.Capabilities).Returns(capabilities);

        return provider;
    }

    private static Mock<IContactCenterVoiceMediaProvider> CreateMediaProvider(string technicalName)
    {
        var provider = new Mock<IContactCenterVoiceMediaProvider>();

        provider.SetupGet(item => item.TechnicalName).Returns(technicalName);

        return provider;
    }
}
