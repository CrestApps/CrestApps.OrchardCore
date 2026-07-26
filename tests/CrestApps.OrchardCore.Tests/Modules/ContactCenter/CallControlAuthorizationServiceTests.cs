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
            Mock.Of<IInteractionManager>(),
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

    [Theory]
    [InlineData(CallControlVerb.Decline)]
    [InlineData(CallControlVerb.Voicemail)]
    public async Task AuthorizeAsync_WhenSystemInitiatedTerminalVerbHasNoCallSessionYet_ResolvesCallIdFromInteraction(CallControlVerb verb)
    {
        // A call rejected at a closed entry point or an unroutable queue is terminated before any provider
        // event has been ingested, so no call session exists and no agent is involved. Requiring either would
        // silently stop the platform from hanging up the caller at the provider.

        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = verb,
            InteractionId = "interaction-1",
            ProviderName = "provider",
            ProviderCallId = "provider-call-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("provider-call-1", result.ProviderCallId);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSystemInitiated_ResolvesProviderCallIdFromServerStateNotTheRequest()
    {
        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "server-owned-call",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("server-owned-call", result.ProviderCallId);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSystemInitiatedRequestNamesADifferentCall_Denies()
    {
        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
            ProviderCallId = "someone-elses-call",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.ProviderCallId);
    }

    [Theory]
    [InlineData(CallControlVerb.Hangup)]
    [InlineData(CallControlVerb.Transfer)]
    [InlineData(CallControlVerb.Dial)]
    [InlineData(CallControlVerb.SupervisorEngage)]
    public async Task AuthorizeAsync_WhenSystemInitiatedVerbIsNotATerminalPlatformAction_Denies(CallControlVerb verb)
    {
        // The system initiator skips the ownership check, so it is restricted to the terminal verbs the
        // platform actually issues. Any other verb must not become reachable by declaring a system initiator.

        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = verb,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSystemInitiatedClaimsSupervisorPrivilege_Denies()
    {
        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
            SupervisorOperation = true,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended)]
    [InlineData(InteractionStatus.Failed)]
    public async Task AuthorizeAsync_WhenSystemInitiatedInteractionIsAlreadyTerminal_Denies(InteractionStatus status)
    {
        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = status,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSystemInitiatedSessionExists_PrefersTheSessionCallId()
    {
        // Arrange
        var service = CreateService(
            interaction: new Interaction
            {
                ItemId = "interaction-1",
                Status = InteractionStatus.Ringing,
                ProviderName = "provider",
                ProviderInteractionId = "provider-call-1",
            },
            session: new CallSession
            {
                InteractionId = "interaction-1",
                AgentId = "agent-1",
                ProviderName = "provider",
                ProviderCallId = "provider-call-1",
                State = ContactCenterCallState.Connected,
            });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Initiator = CallControlInitiator.System,
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("provider-call-1", result.ProviderCallId);
        Assert.Equal("agent-1", result.AgentId);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenInitiatorDefaultsToAgentAndNoUserIsSupplied_Denies()
    {
        // The default initiator must be the restrictive one, so a payload written before the initiator existed
        // is authorized as an agent request and fails closed rather than being treated as a platform action.

        // Arrange
        var service = CreateService(interaction: new Interaction
        {
            ItemId = "interaction-1",
            Status = InteractionStatus.Ringing,
            ProviderName = "provider",
            ProviderInteractionId = "provider-call-1",
        });

        // Act
        var result = await service.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Verb = CallControlVerb.Decline,
            InteractionId = "interaction-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private static CallControlAuthorizationService CreateService(
        Interaction interaction = null,
        CallSession session = null)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(manager => manager.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return new CallControlAuthorizationService(
            Mock.Of<IAgentProfileManager>(),
            callSessionManager.Object,
            interactionManager.Object,
            Mock.Of<ISupervisorQueueAuthorizationService>());
    }
}
