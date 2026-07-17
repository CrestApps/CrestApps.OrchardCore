using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceProviderTests
{
    [Fact]
    public void Capabilities_AdvertiseOnlyExecutableVoiceContracts()
    {
        // Arrange
        var resolver = new Mock<ITelephonyProviderResolver>();
        var service = CreateService(resolver);

        // Act
        var capabilities = service.Capabilities;

        // Assert
        Assert.Equal(ContactCenterVoiceProviderCapabilities.DialerDial, capabilities);
        Assert.Equal(VoiceProviderDeliveryModel.ServerSideAcd, service.DeliveryModel);
        Assert.IsAssignableFrom<IContactCenterVoiceCallControlProvider>(service);
        Assert.IsNotAssignableFrom<IContactCenterVoiceQueueAssignmentProvider>(service);
        Assert.IsNotAssignableFrom<IContactCenterVoiceTransferProvider>(service);
        Assert.IsNotAssignableFrom<IContactCenterVoiceConferenceProvider>(service);
        Assert.IsNotAssignableFrom<IContactCenterVoiceRecordingProvider>(service);
        Assert.IsNotAssignableFrom<IContactCenterVoiceMonitoringProvider>(service);
    }

    [Fact]
    public async Task DialAsync_WhenAsteriskProviderSucceeds_ReturnsProviderCallId()
    {
        // Arrange
        var telephonyProvider = new Mock<ITelephonyProvider>();
        telephonyProvider
            .Setup(provider => provider.DialAsync(
                It.Is<DialRequest>(request => request.To == "+15551234567" && request.From == "+15550001000"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall
            {
                CallId = "call-1",
            }));

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(provider => provider.GetAsync(AsteriskConstants.ProviderTechnicalName))
            .ReturnsAsync(telephonyProvider.Object);

        var service = CreateService(resolver);

        // Act
        var result = await service.DialAsync(new ContactCenterDialRequest
        {
            Destination = "+15551234567",
            CallerId = "+15550001000",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
    }

    [Fact]
    public async Task DialAsync_WhenTenantProviderIsUnavailable_UsesDefaultAsteriskProvider()
    {
        // Arrange
        var telephonyProvider = new Mock<ITelephonyProvider>();
        telephonyProvider
            .Setup(provider => provider.DialAsync(It.IsAny<DialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall
            {
                CallId = "default-call",
            }));

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(provider => provider.GetAsync(AsteriskConstants.ProviderTechnicalName))
            .ReturnsAsync((ITelephonyProvider)null);
        resolver
            .Setup(provider => provider.GetAsync(AsteriskConstants.DefaultProviderTechnicalName))
            .ReturnsAsync(telephonyProvider.Object);

        var service = CreateService(resolver);

        // Act
        var result = await service.DialAsync(new ContactCenterDialRequest
        {
            Destination = "+15551234567",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("default-call", result.ProviderCallId);
        Assert.Equal(AsteriskConstants.DefaultProviderTechnicalName, result.ProviderName);
    }

    [Fact]
    public async Task DialAsync_WhenTelephonyOutcomeIsUnknown_PreservesUnknownOutcome()
    {
        // Arrange
        var telephonyProvider = new Mock<ITelephonyProvider>();
        telephonyProvider
            .Setup(provider => provider.DialAsync(It.IsAny<DialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Unknown("The provider response was lost."));

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(provider => provider.GetAsync(AsteriskConstants.ProviderTechnicalName))
            .ReturnsAsync(telephonyProvider.Object);

        var service = CreateService(resolver);

        // Act
        var result = await service.DialAsync(new ContactCenterDialRequest
        {
            Destination = "+15551234567",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("dial_outcome_unknown", result.ErrorCode);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
    }

    [Fact]
    public async Task ConnectToAgentAsync_FailsClosedUntilAriBridgeIsImplemented()
    {
        // Arrange
        var resolver = new Mock<ITelephonyProviderResolver>();
        var service = CreateService(resolver);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "call-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_bridge_unavailable", result.ErrorCode);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
    }

    private static AsteriskContactCenterVoiceProvider CreateService(Mock<ITelephonyProviderResolver> resolver)
    {
        var localizer = new Mock<IStringLocalizer<AsteriskContactCenterVoiceProvider>>();
        localizer.Setup(localizer => localizer["Asterisk"])
            .Returns(new LocalizedString("Asterisk", "Asterisk"));

        return new AsteriskContactCenterVoiceProvider(
            resolver.Object,
            new TestContactCenterFeatureWorkManager(),
            localizer.Object);
    }
}
