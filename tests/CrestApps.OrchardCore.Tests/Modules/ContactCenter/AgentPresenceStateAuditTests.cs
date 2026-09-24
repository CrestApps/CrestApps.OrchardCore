using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every state change the presence manager makes is recorded exactly once, with the state it left and entered,
/// what caused it, who made it and when it took effect, because payroll is paid from these records.
/// </summary>
public sealed class AgentPresenceStateAuditTests
{
    private static readonly DateTime _now = new(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SignInAsync_FromOffline_RecordsOneSignInByTheAgent_InTheirSession()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Offline);
        var fixture = new Fixture(profile);
        fixture.SessionManager
            .Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSession { ItemId = "s1", UserId = "u1" });

        // Act
        await fixture.Service.SignInAsync("u1", ["q1"], [], TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Offline, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.SignIn, recorded.Change.Source);
        Assert.Equal("a1", recorded.Change.AgentId);
        Assert.Equal("s1", recorded.Change.AgentSessionId);
        Assert.Equal(["q1"], recorded.Change.QueueIds);
        Assert.Equal(_now, recorded.Change.ChangedUtc);
        Assert.Equal(ContactCenterActor.Agent("u1"), recorded.Actor);
    }

    [Fact]
    public async Task UpdateMembershipsAsync_ChangesNoState_AndRecordsNoStateChange()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.UpdateMembershipsAsync("u1", ["q2"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(fixture.Recorder.StateChanges);
    }

    [Fact]
    public async Task SignOutAsync_RecordsOneSignOut_WithTheReasonGiven()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SignOutAsync(
            "u1",
            new AgentStateChangeContext { ReasonName = "site-sign-out" },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Offline, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.SignOut, recorded.Change.Source);
        Assert.Equal("site-sign-out", recorded.Change.ReasonName);
        Assert.Equal(ContactCenterActorType.Agent, recorded.Actor.Type);
    }

    [Fact]
    public async Task MarkOfflineAsync_ForALapsedSession_IsDatedByTheLastHeartbeat_NotTheSweep()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Available);
        profile.PresenceChangedUtc = _now.AddHours(-2);
        var lastHeartbeatUtc = _now.AddSeconds(-137.25);
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.MarkOfflineAsync(
            "u1",
            "session-expired",
            new AgentStateChangeContext
            {
                Source = AgentStateChangeSources.SessionExpired,
                AgentSessionId = "s1",
                ChangedUtc = lastHeartbeatUtc,
            },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Offline, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.SessionExpired, recorded.Change.Source);
        Assert.Equal(lastHeartbeatUtc, recorded.Change.ChangedUtc);
        Assert.Equal(lastHeartbeatUtc, profile.PresenceChangedUtc);
        Assert.Equal("session-expired", recorded.Change.ReasonName);
        Assert.Equal("s1", recorded.Change.AgentSessionId);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Fact]
    public async Task MarkOfflineAsync_WhenTheHeartbeatPrecedesThePreviousChange_NeverDatesTheChangeBeforeIt()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Available);
        var previousChangeUtc = _now.AddSeconds(-10);
        profile.PresenceChangedUtc = previousChangeUtc;
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.MarkOfflineAsync(
            "u1",
            "session-expired",
            new AgentStateChangeContext { ChangedUtc = _now.AddMinutes(-2) },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(previousChangeUtc, recorded.Change.ChangedUtc);
    }

    [Theory]
    [InlineData("Lunch")]
    [InlineData("code-lunch")]
    public async Task SetPresenceAsync_WithAConfiguredReason_RecordsTheReasonCodeById(string postedReason)
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Available);
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Break, postedReason, TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Break, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.SetState, recorded.Change.Source);
        Assert.Equal("code-lunch", recorded.Change.ReasonCodeId);
        Assert.Equal("Lunch", recorded.Change.ReasonName);
        Assert.Equal("code-lunch", profile.PresenceReasonCodeId);
        Assert.Equal("Lunch", profile.PresenceReason);
        Assert.Equal(ContactCenterActor.Agent("u1"), recorded.Actor);
    }

    [Fact]
    public async Task SetPresenceAsync_WithFreeText_RecordsTheTextWithoutAReasonCode()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Away, "Stepped out", TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Null(recorded.Change.ReasonCodeId);
        Assert.Equal("Stepped out", recorded.Change.ReasonName);
    }

    [Fact]
    public async Task SetPresenceAsync_ByAWorkflow_RecordsTheWorkflowAsTheActor()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SetPresenceAsync(
            "u1",
            AgentPresenceStatus.Break,
            reason: null,
            new AgentStateChangeContext { Actor = ContactCenterActor.Workflow("wf-1") },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(ContactCenterActor.Workflow("wf-1"), recorded.Actor);
    }

    [Fact]
    public async Task SetPresenceAsync_SwitchingFromOneBreakToAnother_RecordsTheNewBreak_ButRepeatingOneRecordsNothing()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Break);
        profile.PresenceReason = "Coffee";
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Break, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Break, recorded.Change.CurrentState);
        Assert.Equal("Lunch", recorded.Change.ReasonName);
    }

    [Fact]
    public async Task SetPresenceAsync_DuringACall_RecordsNothing_AndTheRequestTakingEffectIsRecordedAsRequestApplied()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.WrapUp);

        // Routing captured Available to return to when the call was reserved; that alone is not a request.
        profile.RequestedPresenceStatus = AgentPresenceStatus.Available;
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.RequestBreak, "Lunch", TestContext.Current.CancellationToken);
        var recordedWhileOnTheCall = fixture.Recorder.StateChanges.Count;
        fixture.Clock.Advance(TimeSpan.FromSeconds(45));
        await fixture.Service.CompleteWorkAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recordedWhileOnTheCall);
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.WrapUp, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Break, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.RequestApplied, recorded.Change.Source);
        Assert.Equal("code-lunch", recorded.Change.ReasonCodeId);
        Assert.Equal("Lunch", recorded.Change.ReasonName);
        Assert.Null(recorded.Change.RequestedState);
        Assert.Equal(_now.AddSeconds(45), recorded.Change.ChangedUtc);
        Assert.Null(profile.PresenceRequestedUtc);
    }

    [Fact]
    public async Task StartWrapUpAsync_RecordsOneWrapUpStarted_ForTheInteraction()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Busy));
        var hangupUtc = _now.AddMilliseconds(-250);

        // Act
        await fixture.Service.StartWrapUpAsync(
            "a1",
            new AgentStateChangeContext { InteractionId = "int-1", ChangedUtc = hangupUtc },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Busy, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.WrapUp, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.WrapUpStarted, recorded.Change.Source);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(hangupUtc, recorded.Change.ChangedUtc);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Fact]
    public async Task CompleteWorkAsync_WithTheCapturedReturnState_RecordsWorkCompleted()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.WrapUp);
        profile.RequestedPresenceStatus = AgentPresenceStatus.Available;
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.CompleteWorkAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.WorkCompleted, recorded.Change.Source);
        Assert.Null(recorded.Change.ReasonName);
    }

    [Fact]
    public async Task CompleteWorkAsync_WhenWrapUpTimedOut_RecordsTheTimeoutNotACompletion()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.WrapUp));

        // Act
        await fixture.Service.CompleteWorkAsync(
            "a1",
            new AgentStateChangeContext { Source = AgentStateChangeSources.WrapUpTimedOut, InteractionId = "int-1" },
            TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(fixture.Recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.WrapUp, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.WrapUpTimedOut, recorded.Change.Source);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Fact]
    public async Task CompleteWorkAsync_ReturningToReady_DropsTheReasonOfABreakTheAgentTookEarlier()
    {
        // Arrange
        // A break the agent set and cleared long ago left its reason on the profile; only setting a state replaces
        // it. A call ending returned the agent to Available still carrying it, and the presence broadcast said the
        // agent was available for a "Short break".
        var profile = CreateProfile(AgentPresenceStatus.WrapUp);
        profile.RequestedPresenceStatus = AgentPresenceStatus.Available;
        profile.PresenceReason = "Short break";
        profile.PresenceReasonCodeId = "code-short-break";
        var fixture = new Fixture(profile);
        AgentPresenceChangedEventData published = null;
        fixture.Publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((e, _) => published = e.GetData<AgentPresenceChangedEventData>())
            .Returns(Task.CompletedTask);

        // Act
        await fixture.Service.CompleteWorkAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Null(profile.PresenceReason);
        Assert.Null(profile.PresenceReasonCodeId);
        Assert.NotNull(published);
        Assert.Equal(AgentPresenceStatus.Available, published.CurrentStatus);
        Assert.Null(published.Reason);
    }

    [Fact]
    public async Task SetPresenceAsync_ToAvailableWithAReasonOfItsOwn_KeepsThatReason()
    {
        // Arrange
        var profile = CreateProfile(AgentPresenceStatus.Break);
        profile.PresenceReason = "Lunch";
        profile.PresenceReasonCodeId = "code-lunch";
        var fixture = new Fixture(profile);

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Available, "Covering the front desk", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AgentPresenceStatus.Available, profile.PresenceStatus);
        Assert.Equal("Covering the front desk", profile.PresenceReason);
        Assert.Null(profile.PresenceReasonCodeId);
    }

    [Theory]
    [InlineData(AgentStateChangeSources.Reconciled)]
    [InlineData(AgentStateChangeSources.WorkCompleted)]
    public async Task TransitionAsync_ToReadyWithoutAReason_ClearsTheReasonLeftOnTheProfile(string source)
    {
        // Arrange
        var transitions = AgentStateAuditTestDoubles.CreateTransitions();
        var profile = CreateProfile(AgentPresenceStatus.Busy);
        profile.PresenceReason = "Short break";
        profile.PresenceReasonCodeId = "code-short-break";

        // Act
        var change = await transitions.TransitionAsync(profile, AgentPresenceStatus.Available, new AgentStateChangeContext { Source = source }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(profile.PresenceReason);
        Assert.Null(profile.PresenceReasonCodeId);
        Assert.Null(change.ReasonName);
    }

    [Fact]
    public async Task TransitionAsync_ToReadyWhileANotReadyStateIsStillRequested_KeepsTheRequestsReason()
    {
        // Arrange
        // The reason belongs to the break still waiting to take effect, not to the state being entered now.
        var transitions = AgentStateAuditTestDoubles.CreateTransitions();
        var profile = CreateProfile(AgentPresenceStatus.Reserved);
        profile.RequestedPresenceStatus = AgentPresenceStatus.Break;
        profile.PresenceReason = "Lunch";
        profile.PresenceReasonCodeId = "code-lunch";

        // Act
        await transitions.TransitionAsync(profile, AgentPresenceStatus.Available, new AgentStateChangeContext { Source = AgentStateChangeSources.Released }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Lunch", profile.PresenceReason);
        Assert.Equal("code-lunch", profile.PresenceReasonCodeId);
    }

    [Fact]
    public async Task TransitionAsync_ToANotReadyState_KeepsItsReason()
    {
        // Arrange
        var transitions = AgentStateAuditTestDoubles.CreateTransitions();
        var profile = CreateProfile(AgentPresenceStatus.Busy);
        profile.PresenceReason = "Lunch";
        profile.PresenceReasonCodeId = "code-lunch";

        // Act
        await transitions.TransitionAsync(profile, AgentPresenceStatus.Break, new AgentStateChangeContext { Source = AgentStateChangeSources.RequestApplied }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Lunch", profile.PresenceReason);
        Assert.Equal("code-lunch", profile.PresenceReasonCodeId);
    }

    [Fact]
    public async Task TransitionAsync_KeepsEveryPresenceEventTheWritersAlreadyPublish()
    {
        // Arrange
        var fixture = new Fixture(CreateProfile(AgentPresenceStatus.Available));

        // Act
        await fixture.Service.SetPresenceAsync("u1", AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);

        // Assert
        // The audit is recorded beside the presence event, never instead of it: routing and the real-time
        // broadcasts still hear about the change exactly as before.
        fixture.Publisher.Verify(
            p => p.PublishAsync(
                It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.AgentPresenceChanged),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Publisher.Verify(
            p => p.PublishAsync(
                It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.AgentStateChanged),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Single(fixture.Recorder.StateChanges);
    }

    private static AgentProfile CreateProfile(AgentPresenceStatus status)
        => new()
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = status,
            PresenceChangedUtc = _now.AddMinutes(-30),
            AllowedQueueIds = ["q1", "q2"],
            QueueIds = ["q1"],
        };

    private sealed class Fixture
    {
        public Fixture(AgentProfile profile)
        {
            AgentManager
                .Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            AgentManager
                .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            var lunch = new AgentStateReasonCode { ItemId = "code-lunch", Name = "Lunch", AppliesTo = AgentPresenceStatus.Break };
            var reasonCodes = new Mock<IAgentStateReasonCodeManager>();
            reasonCodes.Setup(m => m.FindByNameAsync("Lunch", It.IsAny<CancellationToken>())).ReturnsAsync(lunch);
            reasonCodes.Setup(m => m.FindByIdAsync("code-lunch", It.IsAny<CancellationToken>())).ReturnsAsync(lunch);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));

            Service = new AgentPresenceManagerService(
                AgentManager.Object,
                [SessionManager.Object],
                new NoAgentWorkStateHealingService(),
                new EnforcingAgentEntitlementPolicy(),
                AgentStateAuditTestDoubles.CreateTransitions(Recorder, Clock, reasonCodes.Object),
                Publisher.Object,
                distributedLock.Object,
                Clock,
                NullLogger<AgentPresenceManagerService>.Instance);
        }

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IAgentSessionManager> SessionManager { get; } = new();

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new();

        public RecordingAuditRecorder Recorder { get; } = new();

        public AdvanceableClock Clock { get; } = new(_now);

        public AgentPresenceManagerService Service { get; }
    }
}

/// <summary>
/// A clock a test moves forward by hand.
/// </summary>
internal sealed class AdvanceableClock : IClock
{
    private DateTime _utcNow;

    public AdvanceableClock(DateTime utcNow)
    {
        _utcNow = utcNow;
    }

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);

    public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;

    public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

    public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

    public ITimeZone[] GetTimeZones() => [];
}
