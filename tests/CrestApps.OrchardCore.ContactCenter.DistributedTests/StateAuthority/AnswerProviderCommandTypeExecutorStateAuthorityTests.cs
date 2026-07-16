using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class AnswerProviderCommandTypeExecutorStateAuthorityTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProjectSuccessAsync_WhenProviderReturnsDifferentCallId_PreservesCanonicalProviderCallId()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "provider",
            ProviderInteractionId = "canonical-call-1",
            Status = InteractionStatus.Ringing,
            CreatedUtc = _now,
        };
        var callSession = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "provider",
            ProviderCallId = "canonical-call-1",
            State = ContactCenterCallState.Ringing,
            CreatedUtc = _now,
        };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(service => service.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(service => service.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(callSession);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);
        var executor = new AnswerProviderCommandTypeExecutor(
            Mock.Of<IContactCenterVoiceProviderResolver>(),
            Mock.Of<ITelephonyService>(),
            interactionManager.Object,
            callSessionManager.Object,
            publisher.Object,
            clock.Object);
        var command = CreateCommand();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = "provider",
            ProviderCallId = "less-authoritative-leg-1",
        };

        // Act
        await executor.ProjectSuccessAsync(command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("canonical-call-1", interaction.ProviderInteractionId);
        Assert.Equal("canonical-call-1", callSession.ProviderCallId);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Equal(ContactCenterCallState.Ringing, callSession.State);
        publisher.Verify(
            service => service.PublishAsync(
                It.Is<InteractionEvent>(interactionEvent => interactionEvent.EventType == ContactCenterConstants.Events.CallConnected),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ProviderCommand CreateCommand()
    {
        return new ProviderCommand
        {
            CommandId = "command-1",
            ProviderName = "provider",
            CommandType = ProviderCommandType.Answer,
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            RequestPayload = JsonSerializer.Serialize(new ProviderAnswerCommandRequest
            {
                ActivityId = "activity-1",
                InteractionId = "interaction-1",
                ProviderCallId = "canonical-call-1",
                AgentId = "agent-1",
                AgentUserId = "user-1",
                QueueId = "queue-1",
            }),
        };
    }
}
