using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Covers the agent-facing secure-pause boundary: the recording service secure-pause bookkeeping, the
/// policy-gated agent control service, the safety-net auto-resume guard, and the supervisor monitoring block
/// that keeps a coach out of the secured segment.
/// </summary>
public sealed class ContactCenterSecurePauseTests
{
    [Fact]
    public async Task PauseAsync_WhenApplied_StampsPauseTimestamp()
    {
        // Arrange
        var pausedAt = new DateTime(2026, 3, 4, 8, 30, 0, DateTimeKind.Utc);
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Recording;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateRecordingProvider(RecordingState.Paused);
        var service = CreateRecordingService(interactionManager, provider, new StubClock(pausedAt));

        // Act
        var result = await service.PauseAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(RecordingState.Paused, interaction.RecordingState);
        Assert.Equal(pausedAt, interaction.RecordingPausedUtc);
    }

    [Fact]
    public async Task ResumeAsync_WhenApplied_ClearsPauseTimestampAndReason()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        interaction.RecordingPausedUtc = new DateTime(2026, 3, 4, 8, 30, 0, DateTimeKind.Utc);
        interaction.RecordingPauseReason = "Capturing a card number.";
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateRecordingProvider(RecordingState.Recording);
        var service = CreateRecordingService(interactionManager, provider, new StubClock());

        // Act
        var result = await service.ResumeAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        Assert.Null(interaction.RecordingPausedUtc);
        Assert.Null(interaction.RecordingPauseReason);
    }

    [Fact]
    public async Task AutoResumeAsync_WhenApplied_PublishesAutoResumedEventAndClearsPause()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        interaction.RecordingPausedUtc = new DateTime(2026, 3, 4, 8, 30, 0, DateTimeKind.Utc);
        interaction.RecordingPauseReason = "Capturing a card number.";
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateRecordingProvider(RecordingState.Recording);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateRecordingService(interactionManager, provider, new StubClock(), publisher);

        // Act
        var result = await service.AutoResumeAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        Assert.Null(interaction.RecordingPausedUtc);
        Assert.Null(interaction.RecordingPauseReason);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.RecordingAutoResumed),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AgentPause_WhenSecurePauseDisabled_FailsWithoutTouchingRecording()
    {
        // Arrange
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);
        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = false });

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), "reason", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        recordingService.Verify(
            r => r.PauseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentPause_WhenReasonRequiredButMissing_Fails()
    {
        // Arrange
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);
        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true, RequirePauseReason = true });

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), reason: "  ", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        recordingService.Verify(
            r => r.PauseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentPause_WhenProviderCannotPause_Fails()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);

        // A provider that records but cannot pause must not be offered secure pause, so the boundary refuses
        // before it ever calls the recording service.
        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording);

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), "reason", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        recordingService.Verify(
            r => r.PauseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentPause_WhenNotOwnedByAgent_Fails()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);

        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording | ContactCenterVoiceProviderCapabilities.RecordingPause,
            authorization: FakeCallControlAuthorizationService.Denying());

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), "reason", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        recordingService.Verify(
            r => r.PauseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentPause_WhenAuthorizedAndApplied_PersistsReasonAndNotifies()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        Interaction persisted = null;
        interactionManager
            .Setup(m => m.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Callback<Interaction, System.Text.Json.Nodes.JsonNode, CancellationToken>((value, _, _) => persisted = value)
            .Returns(ValueTask.CompletedTask);
        var recordingService = new Mock<IContactCenterRecordingService>();
        recordingService
            .Setup(r => r.PauseAsync("int1", It.IsAny<CancellationToken>()))
            .Callback(() => interaction.RecordingState = RecordingState.Paused)
            .ReturnsAsync(RecordingCommandResult.Success());
        var notifier = new CapturingRealTimeNotifier();

        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true, RequirePauseReason = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording | ContactCenterVoiceProviderCapabilities.RecordingPause,
            notifier: notifier);

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), " Capturing a card number. ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.IsPaused);
        Assert.Equal("Capturing a card number.", persisted?.RecordingPauseReason);

        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal("int1", notification.InteractionId);
        Assert.True(notification.IsSecurePauseActive);
        Assert.Equal(RecordingState.Paused.ToString(), notification.RecordingState);
    }

    [Fact]
    public async Task AgentResume_WhenAuthorizedAndApplied_NotifiesResumed()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var recordingService = new Mock<IContactCenterRecordingService>();
        recordingService
            .Setup(r => r.ResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());
        var notifier = new CapturingRealTimeNotifier();

        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording | ContactCenterVoiceProviderCapabilities.RecordingPause,
            notifier: notifier);

        // Act
        var result = await service.ResumeAsync("int1", "user1", CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.IsPaused);

        var notification = Assert.Single(notifier.Notifications);
        Assert.False(notification.IsSecurePauseActive);
        Assert.Equal(RecordingState.Recording.ToString(), notification.RecordingState);
    }

    [Fact]
    public async Task AutoResumeGuard_WhenWindowDisabled_DoesNothing()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);
        var service = new SecurePauseAutoResumeService(
            SiteServiceFactory.Create(new ContactCenterRecordingSettings { MaxSecurePauseSeconds = 0 }),
            interactionManager.Object,
            recordingService.Object,
            [],
            new StubClock());

        // Act
        var resumed = await service.ResumeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, resumed);
        interactionManager.Verify(
            m => m.ListPausedRecordingsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AutoResumeGuard_WhenPausesExpired_ResumesEachAndNotifies()
    {
        // Arrange
        var now = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        var expired = new Interaction { ItemId = "int1", AgentId = "agent1", RecordingState = RecordingState.Paused };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.ListPausedRecordingsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([expired]);
        var recordingService = new Mock<IContactCenterRecordingService>();
        recordingService
            .Setup(r => r.AutoResumeAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());
        var notifier = new CapturingRealTimeNotifier();
        var service = new SecurePauseAutoResumeService(
            SiteServiceFactory.Create(new ContactCenterRecordingSettings { MaxSecurePauseSeconds = 300 }),
            interactionManager.Object,
            recordingService.Object,
            [notifier],
            new StubClock(now));

        // Act
        var resumed = await service.ResumeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, resumed);
        recordingService.Verify(r => r.AutoResumeAsync("int1", It.IsAny<CancellationToken>()), Times.Once);

        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal("int1", notification.InteractionId);
        Assert.False(notification.IsSecurePauseActive);
    }

    [Fact]
    public async Task SupervisorEngage_WhileRecordingPaused_FailsClosedBeforeProvider()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Barge);
        var monitoringProvider = provider.As<IContactCenterVoiceMonitoringProvider>();
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallSession)null);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(
            interactionManager.Object,
            callSessionManager.Object,
            resolver.Object,
            publisher.Object,
            CreateCommandExecutor(),
            new FakeCallControlAuthorizationService(),
            new StubClock());

        // Act
        var result = await service.EngageAsync("int1", "sup1", MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        monitoringProvider.Verify(
            p => p.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResumeAsync_WhenNotPaused_FailsWithoutCallingProvider()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Stopped;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateRecordingProvider(RecordingState.Recording);
        var recordingProvider = provider.As<IContactCenterVoiceRecordingProvider>();
        var service = CreateRecordingService(interactionManager, provider, new StubClock());

        // Act
        var result = await service.ResumeAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RecordingState.Stopped, interaction.RecordingState);
        recordingProvider.Verify(
            p => p.SetRecordingStateAsync(It.IsAny<ContactCenterVoiceRecordingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PauseAsync_WhenNotRecording_FailsWithoutCallingProvider()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Stopped;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var provider = CreateRecordingProvider(RecordingState.Paused);
        var recordingProvider = provider.As<IContactCenterVoiceRecordingProvider>();
        var service = CreateRecordingService(interactionManager, provider, new StubClock());

        // Act
        var result = await service.PauseAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(RecordingState.Stopped, interaction.RecordingState);
        recordingProvider.Verify(
            p => p.SetRecordingStateAsync(It.IsAny<ContactCenterVoiceRecordingRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentResume_WhenProviderLacksPauseCapability_Fails()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);

        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording);

        // Act
        var result = await service.ResumeAsync("int1", "user1", CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        recordingService.Verify(
            r => r.ResumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AgentPause_WhenApplied_EvictsActiveSupervisorEngagements()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var recordingService = new Mock<IContactCenterRecordingService>();
        recordingService
            .Setup(r => r.PauseAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingCommandResult.Success());
        var monitoringService = new Mock<IContactCenterMonitoringService>();
        monitoringService
            .Setup(m => m.ForceDisengageAllAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = CreateAgentControlService(
            recordingService,
            settings: new ContactCenterRecordingSettings { AllowAgentSecurePause = true },
            interactionManager: interactionManager,
            capabilities: ContactCenterVoiceProviderCapabilities.Recording | ContactCenterVoiceProviderCapabilities.RecordingPause,
            monitoringService: monitoringService);

        // Act
        var result = await service.PauseAsync("int1", "user1", CreatePrincipal(), "reason", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        monitoringService.Verify(m => m.ForceDisengageAllAsync("int1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForceDisengageAll_WhenSessionsActive_StopsEachAndEndsThem()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var callSession = new CallSession
        {
            InteractionId = "int1",
            AgentId = "agent1",
            MonitorSessions =
            [
                new MonitorSession { MonitorSessionId = "m1", SupervisorUserId = "sup1", Mode = MonitorMode.Monitor, StartedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new MonitorSession { MonitorSessionId = "m2", SupervisorUserId = "sup2", Mode = MonitorMode.Whisper, StartedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            ],
        };
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(callSession);
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Monitor);
        var monitoringProvider = provider.As<IContactCenterVoiceMonitoringProvider>();
        monitoringProvider
            .Setup(p => p.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(
            interactionManager.Object,
            callSessionManager.Object,
            resolver.Object,
            publisher.Object,
            CreateCommandExecutor(),
            new FakeCallControlAuthorizationService(),
            new StubClock());

        // Act
        var stopped = await service.ForceDisengageAllAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, stopped);
        monitoringProvider.Verify(
            p => p.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AutoResumeGuard_WhenWindowExceedsLimit_ClampsToCeiling()
    {
        // Arrange
        var now = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime? capturedCutoff = null;
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.ListPausedRecordingsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((cutoff, _, _) => capturedCutoff = cutoff)
            .ReturnsAsync([]);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);
        var service = new SecurePauseAutoResumeService(
            SiteServiceFactory.Create(new ContactCenterRecordingSettings { MaxSecurePauseSeconds = int.MaxValue }),
            interactionManager.Object,
            recordingService.Object,
            [],
            new StubClock(now));

        // Act
        await service.ResumeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(now.AddSeconds(-ContactCenterRecordingSettings.MaxSecurePauseSecondsLimit), capturedCutoff);
    }

    [Fact]
    public async Task ForceDisengageAll_WhenProviderStopUnconfirmed_LeavesSessionActive()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager.Setup(m => m.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
        var callSession = new CallSession
        {
            InteractionId = "int1",
            AgentId = "agent1",
            MonitorSessions =
            [
                new MonitorSession { MonitorSessionId = "m1", SupervisorUserId = "sup1", Mode = MonitorMode.Monitor, StartedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            ],
        };
        var callSessionManager = new Mock<ICallSessionManager>();
        callSessionManager
            .Setup(m => m.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(callSession);
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Monitor);
        var monitoringProvider = provider.As<IContactCenterVoiceMonitoringProvider>();
        monitoringProvider
            .Setup(p => p.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, OutcomeUnknown = true });
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = new ContactCenterMonitoringService(
            interactionManager.Object,
            callSessionManager.Object,
            resolver.Object,
            publisher.Object,
            CreateCommandExecutor(),
            new FakeCallControlAuthorizationService(),
            new StubClock());

        // Act
        var stopped = await service.ForceDisengageAllAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, stopped);
        Assert.True(callSession.MonitorSessions[0].IsActive);
        publisher.Verify(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ApplyProviderDetails_WhenPausedEventRepeats_KeepsOriginalPauseTimestamp()
    {
        // Arrange
        var firstPausedAt = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc);
        var laterUpdateAt = new DateTime(2026, 3, 4, 8, 10, 0, DateTimeKind.Utc);
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        interaction.RecordingPausedUtc = firstPausedAt;
        var session = new CallSession { InteractionId = "int1" };
        var providerEvent = new ProviderVoiceEvent { RecordingState = RecordingState.Paused };

        // Act
        InvokeApplyProviderDetails(session, interaction, providerEvent, laterUpdateAt);

        // Assert
        Assert.Equal(RecordingState.Paused, interaction.RecordingState);
        Assert.Equal(firstPausedAt, interaction.RecordingPausedUtc);
    }

    [Fact]
    public void ApplyProviderDetails_WhenResumed_ClearsPauseMetadata()
    {
        // Arrange
        var interaction = CreateInteraction();
        interaction.RecordingState = RecordingState.Paused;
        interaction.RecordingPausedUtc = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc);
        interaction.RecordingPauseReason = "Capturing a card number.";
        var session = new CallSession { InteractionId = "int1" };
        var providerEvent = new ProviderVoiceEvent { RecordingState = RecordingState.Recording };

        // Act
        InvokeApplyProviderDetails(session, interaction, providerEvent, new DateTime(2026, 3, 4, 8, 10, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(RecordingState.Recording, interaction.RecordingState);
        Assert.Null(interaction.RecordingPausedUtc);
        Assert.Null(interaction.RecordingPauseReason);
    }

    [Fact]
    public async Task AutoResumeGuard_WhenWindowNegative_DoesNothing()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var recordingService = new Mock<IContactCenterRecordingService>(MockBehavior.Strict);
        var service = new SecurePauseAutoResumeService(
            SiteServiceFactory.Create(new ContactCenterRecordingSettings { MaxSecurePauseSeconds = -30 }),
            interactionManager.Object,
            recordingService.Object,
            [],
            new StubClock());

        // Act
        var resumed = await service.ResumeExpiredAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, resumed);
        interactionManager.Verify(
            m => m.ListPausedRecordingsOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static void InvokeApplyProviderDetails(
        CallSession session,
        Interaction interaction,
        ProviderVoiceEvent providerEvent,
        DateTime now)
    {
        var method = typeof(ProviderVoiceEventService).GetMethod(
            "ApplyProviderDetails",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Invoke(null, [session, interaction, providerEvent, now]);
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "int1",
            ProviderName = "p1",
            ProviderInteractionId = "call-1",
            AgentId = "agent1",
            RecordingState = RecordingState.None,
        };
    }

    private static ClaimsPrincipal CreatePrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user1")], "test"));
    }

    private static Mock<IContactCenterVoiceProvider> CreateRecordingProvider(RecordingState confirmedState)
    {
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(
            ContactCenterVoiceProviderCapabilities.Recording | ContactCenterVoiceProviderCapabilities.RecordingPause);
        provider
            .As<IContactCenterVoiceRecordingProvider>()
            .Setup(p => p.SetRecordingStateAsync(
                It.Is<ContactCenterVoiceRecordingRequest>(request => request.State == confirmedState),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        return provider;
    }

    private static ContactCenterRecordingService CreateRecordingService(
        Mock<IInteractionManager> interactionManager,
        Mock<IContactCenterVoiceProvider> provider,
        StubClock clock,
        Mock<IContactCenterEventPublisher> publisher = null)
    {
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);
        var governance = new Mock<IRecordingGovernancePolicy>();
        governance
            .Setup(p => p.EvaluateStartAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecordingGovernanceDecision.Allow(null, false));

        return new ContactCenterRecordingService(
            interactionManager.Object,
            resolver.Object,
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            CreateCommandExecutor(),
            governance.Object,
            clock);
    }

    private static AgentRecordingControlService CreateAgentControlService(
        Mock<IContactCenterRecordingService> recordingService,
        ContactCenterRecordingSettings settings,
        Mock<IInteractionManager> interactionManager = null,
        ContactCenterVoiceProviderCapabilities capabilities = ContactCenterVoiceProviderCapabilities.None,
        FakeCallControlAuthorizationService authorization = null,
        CapturingRealTimeNotifier notifier = null,
        Mock<IContactCenterMonitoringService> monitoringService = null)
    {
        interactionManager ??= new Mock<IInteractionManager>();
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(p => p.Capabilities).Returns(capabilities);

        if (capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording))
        {
            provider.As<IContactCenterVoiceRecordingProvider>();
        }

        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(r => r.Get("p1")).Returns(provider.Object);

        return new AgentRecordingControlService(
            interactionManager.Object,
            authorization ?? new FakeCallControlAuthorizationService(),
            recordingService.Object,
            (monitoringService ?? new Mock<IContactCenterMonitoringService>()).Object,
            resolver.Object,
            SiteServiceFactory.Create(settings),
            notifier is null ? [] : [notifier],
            new StubClock());
    }

    private static DefaultTelephonyCommandExecutor CreateCommandExecutor()
    {
        return new DefaultTelephonyCommandExecutor(
            Options.Create(new TelephonyCommandOptions()),
            Mock.Of<IHostApplicationLifetime>());
    }

    private sealed class CapturingRealTimeNotifier : IContactCenterRealTimeNotifier
    {
        public List<RecordingStateNotification> Notifications { get; } = [];

        public Task NotifyPresenceChangedAsync(AgentPresenceNotification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyOfferReceivedAsync(AgentOfferNotification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyOfferRevokedAsync(AgentOfferRevokedNotification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyQueueStatsChangedAsync(QueueStatsNotification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyAgentMembershipChangedAsync(
            string userId,
            IEnumerable<string> removedQueueIds,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task NotifyRecordingStateChangedAsync(RecordingStateNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);

            return Task.CompletedTask;
        }
    }
}
