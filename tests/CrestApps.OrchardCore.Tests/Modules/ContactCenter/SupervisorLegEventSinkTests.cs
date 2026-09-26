using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What a supervisor's own leg reports: their phone answering puts them on the call, their phone hanging up ends their
/// engagement -- or, once they took the call over, ends the call -- and a call ending releases every supervisor still
/// listening to it.
/// </summary>
public sealed class SupervisorLegEventSinkTests
{
    [Fact]
    public async Task OnAnswered_MarksTheEngagementConnected_AndTellsTheSupervisor()
    {
        // Arrange
        var session = SupervisorEngagementTests.Session(MonitorMode.Monitor);
        var notifier = new SupervisorEngagementTests.RecordingNotifier();
        var sessions = Sessions(session);
        var sink = CreateSink(session, sessions, notifier);

        // Act
        var answered = await sink.OnAnsweredAsync("p1", "call-1", "sup-leg", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(answered);
        Assert.NotNull(Assert.Single(session.ActiveMonitorSessions).ConnectedUtc);
        sessions.Verify(value => value.UpdateAsync(session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(SupervisorEngagementNotification.Connected, Assert.Single(notifier.Engagements).State);
    }

    [Fact]
    public async Task OnAnswered_ForALegNoEngagementHas_ChangesNothing()
    {
        // Arrange
        var session = SupervisorEngagementTests.Session(MonitorMode.Monitor);
        var sessions = Sessions(session);
        var sink = CreateSink(session, sessions, new SupervisorEngagementTests.RecordingNotifier());

        // Act
        var answered = await sink.OnAnsweredAsync("p1", "call-1", "someone-else", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(answered);
        sessions.Verify(value => value.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnEnded_EndsTheSupervisorsEngagement_WithoutEndingTheCall()
    {
        // Arrange
        var session = SupervisorEngagementTests.Session(MonitorMode.Whisper);
        session.MonitorSessions[0].ConnectedUtc = DateTime.UtcNow;
        var publisher = new Mock<IContactCenterEventPublisher>();
        var failures = new Mock<IContactCenterAgentLegFailureService>();
        var notifier = new SupervisorEngagementTests.RecordingNotifier();
        var sink = CreateSink(session, Sessions(session), notifier, publisher, failures);

        // Act
        var ended = await sink.OnEndedAsync("p1", "call-1", "sup-leg", endedUtc: null, HangupCause.NormalClearing, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ended);
        Assert.Empty(session.ActiveMonitorSessions);
        failures.VerifyNoOtherCalls();
        publisher.Verify(value => value.PublishAsync(
            It.Is<InteractionEvent>(e =>
                e.EventType == ContactCenterConstants.Events.SupervisorMonitorStopped &&
                e.ActorId == "sup1" &&
                e.GetData<Dictionary<string, string>>()["reason"] == "supervisor-left"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(SupervisorEngagementNotification.Ended, Assert.Single(notifier.Engagements).State);
    }

    [Fact]
    public async Task OnEnded_ForASupervisorWhoTookTheCallOver_IsTheAgentHangingUp()
    {
        // Arrange
        var session = SupervisorEngagementTests.Session();
        session.Legs.Add(new CallLeg
        {
            ProviderLegId = "sup-leg",
            Role = CallPartyRole.Agent,
            Status = CallLegStatus.Answered,
            AgentId = "sup-agent",
            StartedUtc = DateTime.UtcNow,
            AnsweredUtc = DateTime.UtcNow,
        });
        var failures = new Mock<IContactCenterAgentLegFailureService>();
        failures
            .Setup(value => value.RecordEndedAsync("p1", "call-1", "sup-leg", It.IsAny<DateTime?>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var publisher = new Mock<IContactCenterEventPublisher>();
        var sink = CreateSink(session, Sessions(session), new SupervisorEngagementTests.RecordingNotifier(), publisher, failures);

        // Act
        var ended = await sink.OnEndedAsync("p1", "call-1", "sup-leg", endedUtc: null, HangupCause.NormalClearing, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ended);
        failures.Verify(value => value.RecordEndedAsync("p1", "call-1", "sup-leg", It.IsAny<DateTime?>(), HangupCause.NormalClearing, It.IsAny<CancellationToken>()), Times.Once);
        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ACallEnding_ReleasesTheSupervisorsStillOnIt_ButNotOnesWhoLeftEarlier()
    {
        // Arrange
        var session = SupervisorEngagementTests.Session(MonitorMode.Monitor);
        var ended = new DateTime(2026, 9, 25, 10, 5, 0, DateTimeKind.Utc);
        session.MonitorSessions.Add(new MonitorSession
        {
            MonitorSessionId = "monitor-2",
            SupervisorUserId = "sup2",
            Mode = MonitorMode.Whisper,
            ProviderLegId = "sup2-leg",
            StartedUtc = ended.AddMinutes(-3),
            EndedUtc = ended.AddMinutes(-2),
        });
        session.TransitionTo(VoiceCallState.Ended);
        session.EndedUtc = ended;
        CallTopologyProjector.EndRemainingMonitorSessions(session, ended);

        var sessions = Sessions(session);
        var provider = SupervisorEngagementTests.MonitoringProvider(out _, out var intervention);
        var resolver = new Mock<IContactCenterVoiceProviderResolver>();
        resolver.Setup(value => value.Get("p1")).Returns(provider.Object);
        var notifier = new SupervisorEngagementTests.RecordingNotifier();
        var handler = new ContactCenterSupervisorLegReleaseHandler(sessions.Object, resolver.Object, [notifier], new StubClock(), NullLogger<ContactCenterSupervisorLegReleaseHandler>.Instance);

        // Act
        await handler.HandleAsync(new InteractionEvent { EventType = ContactCenterConstants.Events.CallEnded, InteractionId = "int1" }, TestContext.Current.CancellationToken);
        await handler.HandleAsync(new InteractionEvent { EventType = ContactCenterConstants.Events.CallConnected, InteractionId = "int1" }, TestContext.Current.CancellationToken);

        // Assert
        intervention.Verify(value => value.ReleaseSupervisorLegAsync("sup-leg", It.IsAny<CancellationToken>()), Times.Once);
        intervention.Verify(value => value.ReleaseSupervisorLegAsync("sup2-leg", It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("sup1", Assert.Single(notifier.Engagements).SupervisorUserId);
    }

    private static Mock<ICallSessionManager> Sessions(CallSession session)
    {
        var sessions = new Mock<ICallSessionManager>();
        sessions.Setup(value => value.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        return sessions;
    }

    private static SupervisorLegEventSink CreateSink(
        CallSession session,
        Mock<ICallSessionManager> sessions,
        SupervisorEngagementTests.RecordingNotifier notifier,
        Mock<IContactCenterEventPublisher> publisher = null,
        Mock<IContactCenterAgentLegFailureService> failures = null)
    {
        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(value => value.FindByProviderInteractionIdAsync("p1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "int1", ProviderName = "p1", ProviderInteractionId = "call-1", AgentId = session.AgentId });

        return new SupervisorLegEventSink(
            interactions.Object,
            sessions.Object,
            (failures ?? new Mock<IContactCenterAgentLegFailureService>()).Object,
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            [notifier],
            new StubClock());
    }
}
