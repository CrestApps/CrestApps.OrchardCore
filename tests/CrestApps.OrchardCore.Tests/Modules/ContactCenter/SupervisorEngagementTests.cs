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
/// A supervisor's engagement as the Contact Center runs it: their own phone told to expect the leg before it is rung,
/// the provider handed the agent's leg to whisper to, a mode change on the same leg without ringing the supervisor
/// again, and a stop that names the supervisor's leg so only it is released.
/// </summary>
public sealed class SupervisorEngagementTests
{
    [Fact]
    public async Task Engage_TellsTheSupervisorsPhoneToExpectTheLeg_BeforeRingingIt_WithTheAgentsLegAndTheSameToken()
    {
        // Arrange
        var order = new List<string>();
        var notifier = new RecordingNotifier(order);
        var provider = MonitoringProvider(out var monitoring, out var intervention);
        ContactCenterVoiceMonitoringRequest engaged = null;
        monitoring
            .Setup(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) =>
            {
                order.Add("engage");
                engaged = request;
            })
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderLegId = "sup-leg" });
        var session = Session();
        var service = CreateService(provider, session, notifier);

        // Act
        var result = await service.EngageAsync("int1", "sup1", null, MonitorMode.Whisper, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(["notify:Requested", "engage"], order);

        var requested = Assert.Single(notifier.Engagements);
        Assert.Equal("sup1", requested.SupervisorUserId);
        Assert.Equal("int1", requested.InteractionId);
        Assert.Equal("Whisper", requested.Mode);
        Assert.Equal("Ann Agent", requested.AgentName);
        Assert.False(string.IsNullOrEmpty(requested.MonitorToken));

        Assert.Equal(requested.MonitorToken, engaged.MonitorToken);
        Assert.Equal("agent-leg", engaged.AgentLegId);

        var live = Assert.Single(session.ActiveMonitorSessions);
        Assert.Equal("sup-leg", live.ProviderLegId);
    }

    [Fact]
    public async Task Engage_WhenTheProviderRefuses_TellsThePhoneTheEngagementEnded()
    {
        // Arrange
        var notifier = new RecordingNotifier();
        var provider = MonitoringProvider(out var monitoring, out _);
        monitoring
            .Setup(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = false, ErrorMessage = "Open your soft phone." });
        var service = CreateService(provider, Session(), notifier);

        // Act
        var result = await service.EngageAsync("int1", "sup1", null, MonitorMode.Monitor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Open your soft phone.", result.Reason);
        Assert.Equal([SupervisorEngagementNotification.Requested, SupervisorEngagementNotification.Ended], notifier.Engagements.Select(value => value.State));
        Assert.Equal("Open your soft phone.", notifier.Engagements[1].Reason);
    }

    [Fact]
    public async Task Stop_NamesTheSupervisorsLegAndTheAgentsLeg_AndTellsThePhone()
    {
        // Arrange
        var notifier = new RecordingNotifier();
        var provider = MonitoringProvider(out var monitoring, out _);
        ContactCenterVoiceMonitoringRequest stopped = null;
        monitoring
            .Setup(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => stopped = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var session = Session(engagedMode: MonitorMode.Monitor);
        var service = CreateService(provider, session, notifier);

        // Act
        var result = await service.StopEngagementAsync("int1", "sup1", null, MonitorMode.Monitor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("sup-leg", stopped.SupervisorLegId);
        Assert.Equal("agent-leg", stopped.AgentLegId);
        Assert.Empty(session.ActiveMonitorSessions);
        Assert.Equal(SupervisorEngagementNotification.Ended, Assert.Single(notifier.Engagements).State);
    }

    [Fact]
    public async Task Stop_WritesTheStopOntoAFreshCopyOfTheCall_NotTheCopyReadBeforeTheProviderWasAsked()
    {
        // Arrange: live, the stop's own restored bridge was reported on the call (call.bridged) while the request was
        // still running, and saving the copy the request had read failed on commit (dashboard/stop answered 500).
        var provider = MonitoringProvider(out var monitoring, out _);
        monitoring
            .Setup(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var readBeforeTheStop = Session(engagedMode: MonitorMode.Monitor);
        var writtenByTheWebhook = Session(engagedMode: MonitorMode.Monitor);
        Func<CallSession, bool> change = null;
        var updater = new Mock<ICallSessionUpdater>();
        updater
            .Setup(value => value.UpdateAsync("int1", It.IsAny<Func<CallSession, bool>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<CallSession, bool>, CancellationToken>((_, mutate, _) => change = mutate)
            .ReturnsAsync(true);
        var service = CreateService(provider, readBeforeTheStop, new RecordingNotifier(), updater: updater.Object);

        // Act
        var result = await service.StopEngagementAsync("int1", "sup1", null, MonitorMode.Monitor, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        updater.Verify(value => value.UpdateAsync("int1", It.IsAny<Func<CallSession, bool>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(readBeforeTheStop.ActiveMonitorSessions);
        Assert.True(change(writtenByTheWebhook));
        Assert.Empty(writtenByTheWebhook.ActiveMonitorSessions);
    }

    [Theory]
    [InlineData(MonitorMode.Monitor, MonitorMode.Whisper)]
    [InlineData(MonitorMode.Whisper, MonitorMode.Barge)]
    [InlineData(MonitorMode.Barge, MonitorMode.Monitor)]
    public async Task SwitchMode_ChangesTheModeOnTheSameLeg_WithoutRingingTheSupervisorAgain(MonitorMode from, MonitorMode to)
    {
        // Arrange
        var notifier = new RecordingNotifier();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = MonitoringProvider(out var monitoring, out var intervention);
        ContactCenterVoiceMonitoringRequest switched = null;
        intervention
            .Setup(value => value.SwitchModeAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => switched = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var session = Session(engagedMode: from);
        var service = CreateService(provider, session, notifier, publisher);

        // Act
        var result = await service.SwitchModeAsync("int1", "sup1", null, to, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(to, switched.Mode);
        Assert.Equal("sup-leg", switched.SupervisorLegId);
        Assert.Equal("agent-leg", switched.AgentLegId);

        monitoring.Verify(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        monitoring.Verify(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        var live = Assert.Single(session.ActiveMonitorSessions);
        Assert.Equal(to, live.Mode);

        // Only a barging supervisor is a party of the conversation.
        var onBridge = session.Bridge?.Participants.Any(participant => participant.ProviderLegId == "sup-leg" && participant.LeftUtc is null) == true;
        Assert.Equal(to == MonitorMode.Barge, onBridge);

        publisher.Verify(
            value => value.PublishAsync(
                It.Is<InteractionEvent>(e =>
                    e.EventType == ContactCenterConstants.Events.SupervisorMonitorModeChanged &&
                    e.ActorId == "sup1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(SupervisorEngagementNotification.ModeChanged, Assert.Single(notifier.Engagements).State);
    }

    [Fact]
    public async Task SwitchMode_DuringASensitiveDataCapture_IsRefused()
    {
        // Arrange
        var provider = MonitoringProvider(out _, out var intervention);
        var service = CreateService(provider, Session(engagedMode: MonitorMode.Monitor), new RecordingNotifier(), recordingState: RecordingState.Paused);

        // Act
        var result = await service.SwitchModeAsync("int1", "sup1", null, MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        intervention.Verify(value => value.SwitchModeAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchMode_WhenNotEngaged_IsRefused()
    {
        // Arrange
        var provider = MonitoringProvider(out _, out var intervention);
        var service = CreateService(provider, Session(), new RecordingNotifier());

        // Act
        var result = await service.SwitchModeAsync("int1", "sup1", null, MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        intervention.Verify(value => value.SwitchModeAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchMode_OnAProviderThatCannotChangeTheRole_StopsAndEngagesAgain()
    {
        // Arrange
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(value => value.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.Monitor | ContactCenterVoiceProviderCapabilities.Barge);
        var monitoring = provider.As<IContactCenterVoiceMonitoringProvider>();
        monitoring
            .Setup(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        monitoring
            .Setup(value => value.EngageAsync(It.Is<ContactCenterVoiceMonitoringRequest>(request => request.Mode == MonitorMode.Barge), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderLegId = "new-leg" });
        var session = Session(engagedMode: MonitorMode.Monitor);
        var service = CreateService(provider, session, new RecordingNotifier());

        // Act
        var result = await service.SwitchModeAsync("int1", "sup1", null, MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        monitoring.Verify(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(MonitorMode.Barge, Assert.Single(session.ActiveMonitorSessions).Mode);
    }

    [Fact]
    public async Task ForceDisengageAll_RecordsWhy_AndNamesEachSupervisorsLeg()
    {
        // Arrange
        var publisher = new Mock<IContactCenterEventPublisher>();
        var provider = MonitoringProvider(out var monitoring, out _);
        ContactCenterVoiceMonitoringRequest stopped = null;
        monitoring
            .Setup(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => stopped = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        var service = CreateService(provider, Session(engagedMode: MonitorMode.Whisper), new RecordingNotifier(), publisher);

        // Act
        var count = await service.ForceDisengageAllAsync("int1", "transfer", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal("sup-leg", stopped.SupervisorLegId);
        publisher.Verify(
            value => value.PublishAsync(
                It.Is<InteractionEvent>(e =>
                    e.EventType == ContactCenterConstants.Events.SupervisorMonitorStopped &&
                    e.GetData<Dictionary<string, string>>()["reason"] == "transfer"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    internal static CallSession Session(MonitorMode? engagedMode = null)
    {
        var session = new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "int1",
            ProviderName = "p1",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            QueueId = "queue-1",
        };

        session.Legs.Add(new CallLeg
        {
            ProviderLegId = "call-1",
            Role = CallPartyRole.Customer,
            Status = CallLegStatus.Answered,
            StartedUtc = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 9, 25, 10, 0, 1, DateTimeKind.Utc),
        });
        session.Legs.Add(new CallLeg
        {
            ProviderLegId = "agent-leg",
            Role = CallPartyRole.Agent,
            Status = CallLegStatus.Answered,
            AgentId = "agent-1",
            StartedUtc = new DateTime(2026, 9, 25, 10, 0, 2, DateTimeKind.Utc),
            AnsweredUtc = new DateTime(2026, 9, 25, 10, 0, 3, DateTimeKind.Utc),
        });

        if (engagedMode.HasValue)
        {
            CallTopologyProjector.StartMonitorSession(
                session,
                "monitor-1",
                "sup1",
                "sup-agent",
                engagedMode.Value,
                new DateTime(2026, 9, 25, 10, 1, 0, DateTimeKind.Utc),
                "sup-leg");
        }

        return session;
    }

    internal static Mock<IContactCenterVoiceProvider> MonitoringProvider(
        out Mock<IContactCenterVoiceMonitoringProvider> monitoring,
        out Mock<IContactCenterVoiceSupervisorInterventionProvider> intervention)
    {
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider.SetupGet(value => value.Capabilities).Returns(
            ContactCenterVoiceProviderCapabilities.Monitor |
            ContactCenterVoiceProviderCapabilities.Whisper |
            ContactCenterVoiceProviderCapabilities.Barge |
            ContactCenterVoiceProviderCapabilities.CallTransfer |
            ContactCenterVoiceProviderCapabilities.Recording);
        monitoring = provider.As<IContactCenterVoiceMonitoringProvider>();
        intervention = provider.As<IContactCenterVoiceSupervisorInterventionProvider>();

        return provider;
    }

    private static ContactCenterMonitoringService CreateService(
        Mock<IContactCenterVoiceProvider> provider,
        CallSession session,
        RecordingNotifier notifier,
        Mock<IContactCenterEventPublisher> publisher = null,
        RecordingState recordingState = RecordingState.None,
        ICallSessionUpdater updater = null)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(value => value.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "int1",
                ProviderName = "p1",
                ProviderInteractionId = "call-1",
                AgentId = "agent-1",
                QueueId = "queue-1",
                RecordingState = recordingState,
            });

        var sessions = new Mock<ICallSessionManager>();
        sessions
            .Setup(value => value.FindByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(value => value.Get("p1")).Returns(provider.Object);

        var agents = new Mock<IAgentProfileManager>();
        agents
            .Setup(value => value.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user", DisplayName = "Ann Agent" });

        return new ContactCenterMonitoringService(
            interactionManager.Object,
            sessions.Object,
            resolver.Object,
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
            new FakeCallControlAuthorizationService(context => new CallControlAuthorizationResult
            {
                Succeeded = true,
                AgentId = "sup-agent",
                ProviderCallId = context.ProviderCallId,
            }),
            new StubClock(),
            [notifier],
            agents.Object,
            updater ?? new InPlaceCallSessionUpdater(interactionManager.Object, sessions.Object));
    }

    internal sealed class RecordingNotifier : ISupervisorEngagementNotifier
    {
        private readonly List<string> _order;

        public RecordingNotifier(List<string> order = null)
        {
            _order = order;
        }

        public List<SupervisorEngagementNotification> Engagements { get; } = [];

        public List<SupervisorMessageNotification> Messages { get; } = [];

        public Task NotifyEngagementAsync(SupervisorEngagementNotification notification, CancellationToken cancellationToken = default)
        {
            _order?.Add("notify:" + notification.State);
            Engagements.Add(notification);

            return Task.CompletedTask;
        }

        public Task NotifyAgentMessageAsync(SupervisorMessageNotification notification, CancellationToken cancellationToken = default)
        {
            Messages.Add(notification);

            return Task.CompletedTask;
        }
    }
}
