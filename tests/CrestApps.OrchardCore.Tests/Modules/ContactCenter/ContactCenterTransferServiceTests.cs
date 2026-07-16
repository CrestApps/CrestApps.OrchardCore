using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterTransferServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TransferAsync_ToQueue_ReEnqueuesActivityAndRecordsHistory()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        var transferProvider = provider.As<IContactCenterVoiceTransferProvider>();
        transferProvider
            .Setup(p => p.TransferAsync(
                It.Is<ContactCenterVoiceTransferRequest>(request =>
                    request.InteractionId == "int-1" &&
                    request.ProviderCallId == "call-1" &&
                    request.TransferType == InteractionTransferType.Blind &&
                    request.TargetType == InteractionTransferTargetType.Queue &&
                    request.Target == "q2"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var service = CreateService(interactionManager, queueService, publisher, CreateResolver(provider));

        var request = new TransferRequest
        {
            InteractionId = "int-1",
            Type = InteractionTransferType.Blind,
            TargetType = InteractionTransferTargetType.Queue,
            TargetId = "q2",
            InitiatedByAgentId = "a1",
        };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        queueService.Verify(s => s.EnqueueAsync("act-1", "q2", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(interaction.TransferHistory);
        Assert.Equal(InteractionStatus.Transferring, interaction.Status);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.InteractionTransferred), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_WhenProviderRejects_DoesNotRecordOrPublish()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        var transferProvider = provider.As<IContactCenterVoiceTransferProvider>();
        transferProvider
            .Setup(p => p.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                Succeeded = false,
                ErrorMessage = "Transfer rejected.",
            });
        var service = CreateService(interactionManager, queueService, publisher, CreateResolver(provider));

        var request = new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.External,
            TargetId = "+15551234567",
        };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Transfer rejected.", result.Reason);
        queueService.Verify(s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(interaction.TransferHistory);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_WhenProviderOutcomeIsUnknown_DoesNotRecordOrPublish()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        var transferProvider = provider.As<IContactCenterVoiceTransferProvider>();
        transferProvider
            .Setup(p => p.TransferAsync(It.IsAny<ContactCenterVoiceTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                OutcomeUnknown = true,
                ErrorMessage = "The provider outcome is unknown.",
            });
        var service = CreateService(interactionManager, queueService, publisher, CreateResolver(provider));

        var request = new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.External,
            TargetId = "+15551234567",
        };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The provider outcome is unknown.", result.Reason);
        Assert.Empty(interaction.TransferHistory);
        Assert.NotEqual(InteractionStatus.Transferring, interaction.Status);
        queueService.Verify(
            s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        interactionManager.Verify(
            m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_WhenCapabilityHasNoExecutableContract_FailsClosed()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        var service = CreateService(interactionManager, queueService, publisher, CreateResolver(provider));
        var request = new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.External,
            TargetId = "+15551234567",
        };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(interaction.TransferHistory);
        queueService.Verify(
            s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_WhenInteractionMissing_Fails()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync((Interaction)null);

        var service = CreateService(
            interactionManager,
            new Mock<IActivityQueueService>(),
            new Mock<IContactCenterEventPublisher>(),
            new Mock<IContactCenterVoiceProviderResolver>());

        var request = new TransferRequest { InteractionId = "int-1", TargetType = InteractionTransferTargetType.Queue, TargetId = "q2" };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private static ContactCenterTransferService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<IActivityQueueService> queueService,
        Mock<IContactCenterEventPublisher> publisher,
        Mock<IContactCenterVoiceProviderResolver> voiceProviderResolver)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterTransferService(
            interactionManager.Object,
            queueService.Object,
            voiceProviderResolver.Object,
            publisher.Object,
            clock.Object);
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "int-1",
            ActivityItemId = "act-1",
            AgentId = "a1",
            ProviderName = "provider",
            ProviderInteractionId = "call-1",
        };
    }

    private static Mock<IContactCenterVoiceProvider> CreateProvider(ContactCenterVoiceProviderCapabilities capabilities)
    {
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(capabilities);

        return provider;
    }

    private static Mock<IContactCenterVoiceProviderResolver> CreateResolver(Mock<IContactCenterVoiceProvider> provider)
    {
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("provider")).Returns(provider.Object);

        return resolver;
    }
}
