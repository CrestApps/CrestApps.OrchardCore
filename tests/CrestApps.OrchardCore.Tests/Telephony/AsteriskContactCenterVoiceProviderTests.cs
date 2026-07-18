using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

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
        Assert.Equal(
            ContactCenterVoiceProviderCapabilities.DialerDial | ContactCenterVoiceProviderCapabilities.AgentConnect,
            capabilities);
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
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
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
    public async Task ConnectToAgentAsync_WhenAgentEndpointMissing_FailsClosed()
    {
        // Arrange
        var resolver = new Mock<ITelephonyProviderResolver>();
        var service = CreateService(resolver);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "call-1",
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = "command-1",
            },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent_endpoint_missing", result.ErrorCode);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WhenProviderCommandIdMissing_FailsClosedWithoutTouchingAri()
    {
        // Arrange
        var resolver = new Mock<ITelephonyProviderResolver>();
        var ariClient = new Mock<IAsteriskAriClient>(MockBehavior.Strict);
        var service = CreateService(resolver, ariClient.Object);

        // Act
        var result = await service.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "call-1",
            AgentEndpoint = "PJSIP/agent1",
            AgentId = "agent-1",
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("command_id_missing", result.ErrorCode);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, result.ProviderName);
        ariClient.VerifyNoOtherCalls();
    }

    private static AsteriskContactCenterVoiceProvider CreateService(
        Mock<ITelephonyProviderResolver> resolver,
        IAsteriskAriClient ariClient = null)
    {
        var localizer = new Mock<IStringLocalizer<AsteriskContactCenterVoiceProvider>>();
        localizer.Setup(localizer => localizer["Asterisk"])
            .Returns(new LocalizedString("Asterisk", "Asterisk"));

        return new AsteriskContactCenterVoiceProvider(
            resolver.Object,
            new TestContactCenterFeatureWorkManager(),
            ariClient ?? Mock.Of<IAsteriskAriClient>(),
            Mock.Of<IAsteriskChannelTenantBindingStore>(),
            new FakeAsteriskPjsipCredentialLeaseStore(),
            new FakeAsteriskAgentChannelReadySignal(),
            Mock.Of<IClock>(),
            NullLogger<AsteriskContactCenterVoiceProvider>.Instance,
            localizer.Object);
    }
}
