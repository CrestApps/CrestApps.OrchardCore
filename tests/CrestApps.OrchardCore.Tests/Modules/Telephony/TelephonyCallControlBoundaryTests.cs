using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

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
                State = ContactCenterCallState.Connected,
            });
        var service = new CallControlAuthorizationService(
            agentManager.Object,
            callSessionManager.Object,
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
}
