using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRecordingAndMonitoringTests
{
    [Fact]
    public async Task StartAsync_WithoutExecutableProviderContract_FailsClosed()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Recording);
        var resolver = CreateResolver(provider);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new ContactCenterRecordingService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(changed);
        Assert.Equal(RecordingState.None, interaction.RecordingState);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WhenExecutableProviderConfirmsSuccess_SetsRecordingStateAndPublishes()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Recording);
        var recordingProvider = provider.As<IContactCenterVoiceRecordingProvider>();
        recordingProvider
            .Setup(p => p.SetRecordingStateAsync(
                It.Is<ContactCenterVoiceRecordingRequest>(request =>
                    request.InteractionId == "int1" &&
                    request.ProviderCallId == "call-1" &&
                    request.State == RecordingState.Recording),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var resolver = CreateResolver(provider);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var service = new ContactCenterRecordingService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.RecordingStarted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenExecutableProviderDoesNotConfirm_DoesNotChangeStateOrPublish()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Recording);
        var recordingProvider = provider.As<IContactCenterVoiceRecordingProvider>();
        recordingProvider
            .Setup(p => p.SetRecordingStateAsync(It.IsAny<ContactCenterVoiceRecordingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                OutcomeUnknown = true,
            });
        var resolver = CreateResolver(provider);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterRecordingService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(changed);
        Assert.Equal(RecordingState.None, interaction.RecordingState);
        interactionManager.Verify(
            m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRecording_DoesNotInvokeProvider()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Recording;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Recording);
        var recordingProvider = provider.As<IContactCenterVoiceRecordingProvider>();
        var resolver = CreateResolver(provider);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterRecordingService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var changed = await service.StartAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(changed);
        recordingProvider.Verify(
            p => p.SetRecordingStateAsync(It.IsAny<ContactCenterVoiceRecordingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EngageAsync_WithCapabilityFlagButNoExecutableOperation_FailsClosed()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Monitor | ContactCenterVoiceProviderCapabilities.Whisper);
        var resolver = CreateResolver(provider);

        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Whisper, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EngageAsync_WhenExecutableProviderConfirmsSuccess_PublishesSuccess()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Barge);
        var monitoringProvider = provider.As<IContactCenterVoiceMonitoringProvider>();
        monitoringProvider
            .Setup(p => p.EngageAsync(
                It.Is<ContactCenterVoiceMonitoringRequest>(request =>
                    request.InteractionId == "int1" &&
                    request.ProviderCallId == "call-1" &&
                    request.SupervisorId == "sup1" &&
                    request.Mode == MonitorMode.Barge),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var resolver = CreateResolver(provider);

        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.SupervisorMonitorStarted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EngageAsync_WhenExecutableProviderDoesNotConfirm_DoesNotPublishSuccess()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Monitor);
        var monitoringProvider = provider.As<IContactCenterVoiceMonitoringProvider>();
        monitoringProvider
            .Setup(p => p.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                OutcomeUnknown = true,
                ErrorMessage = "The provider outcome is unknown.",
            });
        var resolver = CreateResolver(provider);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(interactionManager.Object, resolver.Object, publisher.Object);

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Monitor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The provider outcome is unknown.", result.Reason);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAvailableModesAsync_ReturnsOnlyExecutableAdvertisedModes()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(
            ContactCenterVoiceProviderCapabilities.Monitor |
            ContactCenterVoiceProviderCapabilities.Barge);
        _ = provider.As<IContactCenterVoiceMonitoringProvider>();
        var resolver = CreateResolver(provider);
        var service = new ContactCenterMonitoringService(
            interactionManager.Object,
            resolver.Object,
            new Mock<IContactCenterEventPublisher>().Object);

        // Act
        var modes = await service.GetAvailableModesAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([MonitorMode.Monitor, MonitorMode.Barge], modes);
    }

    [Fact]
    public async Task GetAvailableModesAsync_WithoutProviderCallId_ReturnsNoModes()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.ProviderInteractionId = null;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.Monitor);
        _ = provider.As<IContactCenterVoiceMonitoringProvider>();
        var resolver = CreateResolver(provider);
        var service = new ContactCenterMonitoringService(
            interactionManager.Object,
            resolver.Object,
            new Mock<IContactCenterEventPublisher>().Object);

        // Act
        var modes = await service.GetAvailableModesAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(modes);
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "int1",
            ProviderName = "p1",
            ProviderInteractionId = "call-1",
            RecordingState = RecordingState.None,
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
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);

        return resolver;
    }
}
