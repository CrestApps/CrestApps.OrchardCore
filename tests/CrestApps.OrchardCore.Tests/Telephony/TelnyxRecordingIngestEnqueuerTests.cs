using System.Text;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxRecordingIngestEnqueuerTests
{
    private static readonly DateTime _now = new(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_VoicemailRecording_FlagsInteractionAndPublishesProjection()
    {
        // Arrange: a saved recording tagged as a voicemail for user-1's interaction.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            AgentId = null,
            CorrelationId = "corr-1",
            ProviderInteractionId = "call-1",
            TechnicalMetadata = new Dictionary<string, object>(),
        };

        var callEvent = new TelnyxCallEvent
        {
            RecordingId = "rec-1",
            ClientState = DecodeClientState(TelnyxRecordingClientState.ForVoicemail("interaction-1", "user-1").ToClientState()),
        };

        var (jobStore, interactionManager, agentManager, publisher, clock) = CreateMocks(interaction);

        var handler = new TelnyxRecordingIngestEnqueuer(
            jobStore.Object,
            interactionManager.Object,
            [agentManager.Object],
            publisher.Object,
            Mock.Of<IContactCenterScopeExecutor>(),
            clock.Object,
            NullLogger<TelnyxRecordingIngestEnqueuer>.Instance);

        InteractionEvent publishedEvent = null;
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvent = interactionEvent)
            .Returns(Task.CompletedTask);

        // Act
        var handled = await handler.HandleAsync(callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.True((bool)interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey]);
        Assert.Equal("agent-1", interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.RecipientAgentMetadataKey]);
        Assert.Equal("rec-1", interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference]);
        Assert.NotNull(publishedEvent);
        Assert.Equal(ContactCenterConstants.Events.CallSentToVoicemail, publishedEvent.EventType);
        Assert.Equal("interaction-1", publishedEvent.InteractionId);
    }

    [Fact]
    public async Task HandleAsync_NonVoicemailRecording_DoesNotFlagOrPublish()
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            TechnicalMetadata = new Dictionary<string, object>(),
        };

        var callEvent = new TelnyxCallEvent
        {
            RecordingId = "rec-1",
            ClientState = DecodeClientState(TelnyxRecordingClientState.ForInteraction("interaction-1").ToClientState()),
        };

        var (jobStore, interactionManager, agentManager, publisher, clock) = CreateMocks(interaction);

        var handler = new TelnyxRecordingIngestEnqueuer(
            jobStore.Object,
            interactionManager.Object,
            [agentManager.Object],
            publisher.Object,
            Mock.Of<IContactCenterScopeExecutor>(),
            clock.Object,
            NullLogger<TelnyxRecordingIngestEnqueuer>.Instance);

        var handled = await handler.HandleAsync(callEvent, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.Voicemail.ProjectionMetadataKey));
        publisher.Verify(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (
        Mock<ITelnyxRecordingIngestJobStore>,
        Mock<IInteractionManager>,
        Mock<IAgentProfileManager>,
        Mock<IContactCenterEventPublisher>,
        Mock<IClock>) CreateMocks(Interaction interaction)
    {
        var jobStore = new Mock<ITelnyxRecordingIngestJobStore>();
        jobStore
            .Setup(store => store.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return (jobStore, interactionManager, agentManager, publisher, clock);
    }

    private static string DecodeClientState(string base64ClientState)
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64ClientState));
}
