using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingAccessGovernanceServiceTests
{
    [Fact]
    public async Task RecordAccessAsync_WhenRecordingPresent_PublishesAccessAuditEvent()
    {
        // Arrange
        var interaction = CreateInteraction(recordingReference: "storage/int1");
        var interactionManager = CreateInteractionManager(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();
        InteractionEvent published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock());

        // Act
        var audited = await service.RecordAccessAsync("int1", "supervisor-1", "quality-review", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(audited);
        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.RecordingAccessed, published.EventType);
        Assert.Equal("supervisor-1", published.ActorId);

        var data = published.GetData<RecordingAccessedEventData>();

        Assert.Equal("supervisor-1", data.ActorId);
        Assert.Equal("quality-review", data.Purpose);
        Assert.Equal("storage/int1", data.RecordingReference);
    }

    [Fact]
    public async Task RecordAccessAsync_WhenNoRecordingReference_DoesNotAudit()
    {
        // Arrange
        var interaction = CreateInteraction(recordingReference: null);
        var interactionManager = CreateInteractionManager(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock());

        // Act
        var audited = await service.RecordAccessAsync("int1", "supervisor-1", "quality-review", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(audited);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EraseAsync_WhenNotUnderLegalHold_ClearsReferenceAndPublishesErasedEvent()
    {
        // Arrange
        var erasureInstant = new DateTime(2031, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var interaction = CreateInteraction(recordingReference: "storage/int1");
        interaction.TechnicalMetadata = new Dictionary<string, object>
        {
            [ContactCenterConstants.RecordingMetadata.RecordingName] = "rec-int1",
            [ContactCenterConstants.RecordingMetadata.StorageReference] = "storage/int1",
            [ContactCenterConstants.RecordingMetadata.Format] = "wav",
            [ContactCenterConstants.RecordingMetadata.RetrievalPath] = "recordings/stored/rec-int1",
            ["unrelated"] = "keep-me",
        };
        var interactionManager = CreateInteractionManager(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();
        InteractionEvent published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock(erasureInstant));

        // Act
        var decision = await service.EraseAsync("int1", "dpo-1", "gdpr-subject-request", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.Erased);
        Assert.Null(interaction.RecordingReference);
        Assert.Equal(erasureInstant, interaction.RecordingErasedUtc);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.RecordingMetadata.RecordingName));
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.RecordingMetadata.RetrievalPath));
        Assert.Equal("keep-me", interaction.TechnicalMetadata["unrelated"]);
        interactionManager.Verify(m => m.UpdateAsync(interaction, null, It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.RecordingErased, published.EventType);

        var data = published.GetData<RecordingErasedEventData>();

        Assert.Equal("dpo-1", data.ActorId);
        Assert.Equal("gdpr-subject-request", data.Reason);
        Assert.Equal("storage/int1", data.RecordingReference);
    }

    [Fact]
    public async Task EraseAsync_WhenUnderLegalHold_DeniesAndDoesNotClearReference()
    {
        // Arrange
        var interaction = CreateInteraction(recordingReference: "storage/int1");
        interaction.RecordingLegalHold = true;
        var interactionManager = CreateInteractionManager(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();
        InteractionEvent published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock());

        // Act
        var decision = await service.EraseAsync("int1", "dpo-1", "gdpr-subject-request", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Erased);
        Assert.Equal(ContactCenterConstants.RecordingErasureDenyReason.LegalHold, decision.DenyReasonCode);
        Assert.Equal("storage/int1", interaction.RecordingReference);
        Assert.Null(interaction.RecordingErasedUtc);
        interactionManager.Verify(m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.RecordingErasureDenied, published.EventType);

        var data = published.GetData<RecordingErasureDeniedEventData>();

        Assert.Equal(ContactCenterConstants.RecordingErasureDenyReason.LegalHold, data.DenyReasonCode);
    }

    [Fact]
    public async Task EraseAsync_WhenInteractionExistsButNoRecordingReference_DeniesAndAuditsNoRecording()
    {
        // Arrange
        var interaction = CreateInteraction(recordingReference: null);
        var interactionManager = CreateInteractionManager(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();
        InteractionEvent published = null;
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock());

        // Act
        var decision = await service.EraseAsync("int1", "dpo-1", "gdpr-subject-request", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Erased);
        Assert.Equal(ContactCenterConstants.RecordingErasureDenyReason.NoRecording, decision.DenyReasonCode);

        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.RecordingErasureDenied, published.EventType);

        var data = published.GetData<RecordingErasureDeniedEventData>();

        Assert.Equal("dpo-1", data.ActorId);
        Assert.Equal(ContactCenterConstants.RecordingErasureDenyReason.NoRecording, data.DenyReasonCode);
    }

    [Fact]
    public async Task EraseAsync_WhenInteractionNotFound_DeniesWithoutAudit()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.FindByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new RecordingAccessGovernanceService(interactionManager.Object, publisher.Object, CreateClock());

        // Act
        var decision = await service.EraseAsync("missing", "dpo-1", "gdpr-subject-request", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.Erased);
        Assert.Equal(ContactCenterConstants.RecordingErasureDenyReason.NoRecording, decision.DenyReasonCode);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IInteractionManager> CreateInteractionManager(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        return interactionManager;
    }

    private static IClock CreateClock(DateTime? utcNow = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(utcNow ?? new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        return clock.Object;
    }

    private static Interaction CreateInteraction(string recordingReference)
    {
        return new Interaction
        {
            ItemId = "int1",
            ProviderName = "p1",
            ProviderInteractionId = "call-1",
            AgentId = "agent-1",
            RecordingReference = recordingReference,
            RecordingState = RecordingState.Stopped,
        };
    }
}
