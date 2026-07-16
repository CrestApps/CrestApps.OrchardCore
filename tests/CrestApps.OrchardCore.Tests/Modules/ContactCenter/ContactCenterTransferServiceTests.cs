using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    public async Task TransferAsync_WhenCallerDisconnectsDuringProviderMutation_CompletesWithServerOwnedToken()
    {
        // Arrange
        using var callerCancellation = new CancellationTokenSource();
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback<Interaction, System.Text.Json.Nodes.JsonNode, CancellationToken>(
                (_, _, cancellationToken) => Assert.Equal(CancellationToken.None, cancellationToken))
            .Returns(ValueTask.CompletedTask);
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(service => service.EnqueueAsync(
                "act-1",
                "q2",
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, string, InteractionPriority?, CancellationToken>(
                (_, _, _, cancellationToken) => Assert.Equal(CancellationToken.None, cancellationToken))
            .ReturnsAsync(new QueueItem());
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(
                It.IsAny<InteractionEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>(
                (_, cancellationToken) => Assert.Equal(CancellationToken.None, cancellationToken))
            .Returns(Task.CompletedTask);
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        provider
            .As<IContactCenterVoiceTransferProvider>()
            .Setup(value => value.TransferAsync(
                It.IsAny<ContactCenterVoiceTransferRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns<ContactCenterVoiceTransferRequest, CancellationToken>((_, cancellationToken) =>
            {
                Assert.True(cancellationToken.CanBeCanceled);
                Assert.False(cancellationToken.IsCancellationRequested);
                Assert.NotEqual(callerCancellation.Token, cancellationToken);
                callerCancellation.Cancel();

                return Task.FromResult(new ContactCenterVoiceProviderResult { Succeeded = true });
            });
        var service = CreateService(
            interactionManager,
            queueService,
            publisher,
            CreateResolver(provider));

        // Act
        var result = await service.TransferAsync(new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.Queue,
            TargetId = "q2",
        }, callerCancellation.Token);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(callerCancellation.IsCancellationRequested);
        Assert.Equal(InteractionStatus.Transferring, interaction.Status);
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
    public async Task TransferAsync_WhenProviderDeadlineExpires_ReturnsUnknownWithoutRecording()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var queueService = new Mock<IActivityQueueService>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = CreateProvider(ContactCenterVoiceProviderCapabilities.CallTransfer);
        _ = provider.As<IContactCenterVoiceTransferProvider>();
        var service = CreateService(
            interactionManager,
            queueService,
            publisher,
            CreateResolver(provider),
            new TimeoutTelephonyCommandExecutor());

        // Act
        var result = await service.TransferAsync(new TransferRequest
        {
            InteractionId = "int-1",
            TargetType = InteractionTransferTargetType.Queue,
            TargetId = "q2",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Contains("outcome is unknown", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(interaction.TransferHistory);
        queueService.Verify(
            value => value.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<InteractionPriority?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        Mock<IContactCenterVoiceProviderResolver> voiceProviderResolver,
        ITelephonyCommandExecutor commandExecutor = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterTransferService(
            interactionManager.Object,
            queueService.Object,
            voiceProviderResolver.Object,
            publisher.Object,
            commandExecutor ?? new DefaultTelephonyCommandExecutor(
                Options.Create(new TelephonyCommandOptions()),
                Mock.Of<IHostApplicationLifetime>()),
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

    private sealed class TimeoutTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
        {
            return Task.FromException<TResult>(new TimeoutException());
        }
    }
}
