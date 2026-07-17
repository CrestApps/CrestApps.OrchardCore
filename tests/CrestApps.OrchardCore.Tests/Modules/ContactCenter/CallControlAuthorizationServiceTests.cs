using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CallControlAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_WhenAgentDoesNotOwnCallSession_DeniesWithoutProviderLeak()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-2", UserId = "user-2" });
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallSession
            {
                InteractionId = "interaction-1",
                AgentId = "agent-1",
                ProviderName = "provider",
                ProviderCallId = "provider-call-1",
                State = ContactCenterCallState.Connected,
            });
        var service = new CallControlAuthorizationService(
            agentManager.Object,
            callSessionManager.Object,
            Mock.Of<ISupervisorQueueAuthorizationService>());

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            UserId = "user-2",
            Verb = CallControlVerb.Hangup,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested call is not available.", result.FailureReason);
        Assert.Null(result.ProviderCallId);
    }
}
