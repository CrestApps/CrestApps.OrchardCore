using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRecordingAndMonitoringTests
{
    [Fact]
    public async Task StartAsync_SetsRecordingStateAndPublishes()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "int1", RecordingState = RecordingState.None };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new ContactCenterRecordingService(interactionManager.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.RecordingStarted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRecording_DoesNothing()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "int1", RecordingState = RecordingState.Recording };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new ContactCenterRecordingService(interactionManager.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(changed);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EngageAsync_WhenProviderSupportsMode_Succeeds()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "int1", ProviderName = "p1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Monitor | ContactCenterVoiceProviderCapabilities.Whisper);
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);

        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Whisper, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.SupervisorMonitorStarted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EngageAsync_WhenProviderLacksCapability_Fails()
    {
        // Arrange
        var interaction = new Interaction { ItemId = "int1", ProviderName = "p1" };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Monitor);
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);

        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
