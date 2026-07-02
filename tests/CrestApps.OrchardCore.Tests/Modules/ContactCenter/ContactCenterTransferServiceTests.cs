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
        var interaction = new Interaction { ItemId = "int-1", ActivityItemId = "act-1", AgentId = "a1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(interactionManager, queueService, publisher);

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
    public async Task TransferAsync_ToExternal_RecordsHistoryWithoutReEnqueue()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "int-1", ActivityItemId = "act-1", AgentId = "a1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var queueService = new Mock<IActivityQueueService>();
        var service = CreateService(interactionManager, queueService, new Mock<IContactCenterEventPublisher>());

        var request = new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.External,
            TargetId = "+15551234567",
        };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        queueService.Verify(s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(interaction.TransferHistory);
    }

    [Fact]
    public async Task TransferAsync_WhenInteractionMissing_Fails()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int-1", It.IsAny<CancellationToken>())).ReturnsAsync((Interaction)null);

        var service = CreateService(interactionManager, new Mock<IActivityQueueService>(), new Mock<IContactCenterEventPublisher>());

        var request = new TransferRequest { InteractionId = "int-1", TargetType = InteractionTransferTargetType.Queue, TargetId = "q2" };

        // Act
        var result = await service.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private static ContactCenterTransferService CreateService(
        Mock<IInteractionManager> interactionManager,
        Mock<IActivityQueueService> queueService,
        Mock<IContactCenterEventPublisher> publisher)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterTransferService(interactionManager.Object, queueService.Object, publisher.Object, clock.Object);
    }
}
