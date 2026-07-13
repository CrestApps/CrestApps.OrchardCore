using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterVoiceMediaProviderResolverTests
{
    [Fact]
    public void Get_WhenProviderAdvertisesBidirectionalMedia_ReturnsMatchingMediaProvider()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "asterisk",
            ContactCenterVoiceProviderCapabilities.DialerDial |
            ContactCenterVoiceProviderCapabilities.BidirectionalMedia);
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
    public void Get_WhenProviderDoesNotAdvertiseBidirectionalMedia_ReturnsNull()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "dialpad",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var mediaProvider = CreateMediaProvider("dialpad");
        var voiceProviderResolver = CreateVoiceProviderResolver(voiceProvider);
        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            [mediaProvider.Object]);

        // Act
        var result = resolver.Get("dialpad");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_WhenProviderAdvertisesCapabilityWithoutImplementation_ReturnsNull()
    {
        // Arrange
        var voiceProvider = CreateVoiceProvider(
            "provider",
            ContactCenterVoiceProviderCapabilities.BidirectionalMedia);
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
    public void GetAll_ReturnsOnlyProvidersWithAdvertisedCapabilityAndImplementation()
    {
        // Arrange
        var supportedVoiceProvider = CreateVoiceProvider(
            "asterisk",
            ContactCenterVoiceProviderCapabilities.BidirectionalMedia);
        var unsupportedVoiceProvider = CreateVoiceProvider(
            "dialpad",
            ContactCenterVoiceProviderCapabilities.DialerDial);
        var supportedMediaProvider = CreateMediaProvider("asterisk");
        var unsupportedMediaProvider = CreateMediaProvider("dialpad");
        var orphanedMediaProvider = CreateMediaProvider("unregistered");
        var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();

        voiceProviderResolver
            .Setup(resolver => resolver.GetAll())
            .Returns([supportedVoiceProvider.Object, unsupportedVoiceProvider.Object]);

        var resolver = new ContactCenterVoiceMediaProviderResolver(
            voiceProviderResolver.Object,
            [supportedMediaProvider.Object, unsupportedMediaProvider.Object, orphanedMediaProvider.Object]);

        // Act
        var results = resolver.GetAll();

        // Assert
        Assert.Collection(
            results,
            provider => Assert.Same(supportedMediaProvider.Object, provider));
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
