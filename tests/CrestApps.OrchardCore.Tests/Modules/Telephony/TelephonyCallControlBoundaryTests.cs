using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using TelephonyTransferRequest = CrestApps.OrchardCore.Telephony.Models.TransferRequest;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class TelephonyCallControlBoundaryTests
{
    [Fact]
    public async Task SharedBoundary_WhenMergeParticipantIsOwnedByAnotherAgent_Denies()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "interaction-2",
                AgentId = "agent-2",
                ProviderCallId = "provider-call-2",
            }.RestorePersistedState(VoiceCallState.Connected));
        var service = new CallControlAuthorizationService(
            agentManager.Object,
            callSessionManager.Object,
            Mock.Of<IInteractionManager>(),
            Mock.Of<ISupervisorQueueAuthorizationService>());

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            UserId = "user-1",
            Verb = CallControlVerb.Merge,
            InteractionId = "interaction-2",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("911")]
    [InlineData("112")]
    [InlineData("+19005551234")]
    public async Task DialAsync_WhenTheKeypadTargetsARefusedDestination_DoesNotReachTheProvider(string destination)
    {
        // Arrange
        // The soft-phone keypad reaches ITelephonyService.DialAsync directly, which is how the emergency and
        // premium policy used to be bypassed entirely.
        var provider = new RecordingTelephonyProvider { ResultToReturn = TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }) };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new DefaultOutboundCallScreeningService([]),
            new StubTelephonyExtensionResolver(),
            DialDestinationPolicyFactory.Create(),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(new DialRequest { To = destination }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(provider.LastOperation);
    }

    [Theory]
    [InlineData("911")]
    [InlineData("+19005551234")]
    public async Task TransferAsync_WhenTheTransferFieldTargetsARefusedDestination_DoesNotReachTheProvider(string destination)
    {
        // Arrange
        var provider = new RecordingTelephonyProvider { ResultToReturn = TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }) };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new DefaultOutboundCallScreeningService([]),
            new StubTelephonyExtensionResolver(),
            DialDestinationPolicyFactory.Create(),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.TransferAsync(
            new TelephonyTransferRequest { CallId = "call-1", To = destination },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(provider.LastOperation);
    }

    [Fact]
    public async Task DialAsync_WhenTheDestinationMerelyEndsInAnEmergencyCode_ReachesTheProvider()
    {
        // Arrange
        var provider = new RecordingTelephonyProvider { ResultToReturn = TelephonyResult.Success(new TelephonyCall { CallId = "call-1" }) };
        var service = new DefaultTelephonyService(
            new StubTelephonyProviderResolver(provider),
            new DefaultOutboundCallScreeningService([]),
            new StubTelephonyExtensionResolver(),
            DialDestinationPolicyFactory.Create(),
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await service.DialAsync(
            new DialRequest { To = "+14255550911" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Dial", provider.LastOperation);
    }
}
