using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentAvailabilityRecoveryServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecoverAsync_WhenWrapUpIsWithinDeadline_DoesNotReleaseAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-5));
        var presenceManager = new Mock<IAgentPresenceManager>();
        var service = CreateService(agent, interaction, presenceManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecoverAsync_WhenWrapUpDeadlineExpired_CompletesInteractionAndReleasesAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-16));
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.GetPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([interaction]);
        var service = CreateService(agent, interaction, presenceManager, interactionManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(_now, interaction.WrapUpCompletedUtc);
        interactionManager.Verify(
            manager => manager.UpdateAsync(interaction, null, It.IsAny<CancellationToken>()),
            Times.Once);

        // The platform ended wrap-up that ran past its limit, which the audit must tell apart from the agent
        // finishing their work.
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync(
                "a1",
                It.Is<AgentStateChangeContext>(context =>
                    context.Source == AgentStateChangeSources.WrapUpTimedOut &&
                    context.InteractionId == "i1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenWrapUpHasNoInteraction_ReleasesOrphanedAgent()
    {
        // Arrange
        var agent = CreateAgent();
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var service = CreateService(agent, null, presenceManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenEveryPendingWrapUpExpired_CompletesAllInteractions()
    {
        // Arrange
        var agent = CreateAgent();
        var first = CreateInteraction(_now.AddMinutes(-20));
        var second = CreateInteraction(_now.AddMinutes(-16));
        second.ItemId = "i2";
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", PresenceStatus = AgentPresenceStatus.Available });
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.GetPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var service = CreateService(agent, null, presenceManager, interactionManager);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(_now, first.WrapUpCompletedUtc);
        Assert.Equal(_now, second.WrapUpCompletedUtc);
        interactionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RecoverAsync_WhenRunAgainAfterRecovery_DoesNotRepeatTransitions()
    {
        // Arrange
        var agent = CreateAgent();
        var interaction = CreateInteraction(_now.AddMinutes(-16));
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => agent.PresenceStatus == AgentPresenceStatus.WrapUp ? [agent] : []);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.GetPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => interaction.WrapUpCompletedUtc.HasValue ? [] : [interaction]);
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                agent.PresenceStatus = AgentPresenceStatus.Available;

                return agent;
            });
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var service = new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IInteractionEventStore>().Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);

        // Act
        var firstPass = await service.RecoverAsync(TestContext.Current.CancellationToken);
        var secondPass = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, firstPass);
        Assert.Equal(0, secondPass);
        interactionManager.Verify(
            manager => manager.UpdateAsync(interaction, null, It.IsAny<CancellationToken>()),
            Times.Once);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverAsync_WhenOneAgentLockIsContended_ContinuesWithRemainingAgents()
    {
        // Arrange
        var first = CreateAgent();
        var second = new AgentProfile
        {
            ItemId = "a2",
            PresenceStatus = AgentPresenceStatus.WrapUp,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.GetPendingWrapUpsByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The profile is locked."));
        presenceManager
            .Setup(manager => manager.CompleteWorkAsync("a2", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a2", PresenceStatus = AgentPresenceStatus.Available });
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var service = new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IInteractionEventStore>().Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);

        // Act
        var recovered = await service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        presenceManager.Verify(
            manager => manager.CompleteWorkAsync("a2", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Live: a queued callback's agent leg failed and the agent stayed Busy with nothing on the line, and a supervisor's
    // Set Available waited for work that would never end. A Busy agent whose accepted call is over is put right once
    // the grace period has passed.
    [Fact]
    public async Task RecoverAsync_ABusyAgentWhoseAcceptedCallIsOver_IsReturnedToWork()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFromAcceptedCall(InteractionStatus.Failed, endedUtc: _now.AddMinutes(-2));

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        fixture.Presence.Verify(
            manager => manager.CompleteWorkAsync(
                "busy-agent",
                It.Is<AgentStateChangeContext>(context => context.Source == AgentStateChangeSources.Reconciled && context.InteractionId == "interaction-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Live: an agent's day of test calls left six accepted reservations behind, one of them an offer whose call was
    // never placed, and that one kept the agent Busy for good. Only the offer that made them Busy counts.
    [Fact]
    public async Task RecoverAsync_AnOlderAcceptedOfferThatNeverGotACall_DoesNotKeepTheAgentBusy()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.Reservations
            .Setup(manager => manager.GetActiveByAgentAsync("busy-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityReservation { ItemId = "stale", ActivityItemId = "never-dialled", AgentId = "busy-agent" }.RestorePersistedStatus(ReservationStatus.Accepted)]);
        fixture.BusyFromAcceptedCall(InteractionStatus.Ended, endedUtc: _now.AddMinutes(-5));

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
    }

    // An accept recorded before the interaction existed names only the reservation, which leads to the call.
    [Fact]
    public async Task RecoverAsync_ABusyChangeNamingOnlyTheReservation_IsTracedToItsCall()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFromAcceptedCall(InteractionStatus.Ended, endedUtc: _now.AddMinutes(-2), namesInteraction: false);

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
    }

    [Theory]
    [InlineData(InteractionStatus.Connected)]
    [InlineData(InteractionStatus.Ringing)]
    public async Task RecoverAsync_ABusyAgentOnALiveCall_IsLeftAlone(InteractionStatus status)
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFromAcceptedCall(status, endedUtc: null);

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        fixture.AssertNotReleased();
    }

    // A preview dial or a callback the agent accepted has no call until the dial is placed.
    [Fact]
    public async Task RecoverAsync_ABusyAgentWhoseAcceptedOfferHasNoCallYet_IsLeftAlone()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFrom(new AgentStateChangedEventData { CurrentState = AgentPresenceStatus.Busy, Source = AgentStateChangeSources.Accepted, ReservationId = "reservation-1" });
        fixture.Reservations
            .Setup(manager => manager.FindByIdAsync("reservation-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "reservation-1", ActivityItemId = "activity-1", AgentId = "busy-agent" });

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        fixture.AssertNotReleased();
    }

    // Answering a colleague's consult, or taking a call over, makes an agent Busy on a call that is still going.
    [Theory]
    [InlineData(AgentStateChangeSources.ConsultAnswered)]
    [InlineData(AgentStateChangeSources.SupervisorTakeover)]
    public async Task RecoverAsync_ABusyAgentInTheMiddleOfAConsultOrTakeOver_IsLeftAlone(string source)
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFrom(new AgentStateChangedEventData { CurrentState = AgentPresenceStatus.Busy, Source = source, InteractionId = "colleague-call" });
        fixture.Interactions
            .Setup(manager => manager.FindByIdAsync("colleague-call", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "colleague-call", AgentId = "colleague" }.RestorePersistedStatus(InteractionStatus.Connected));

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        fixture.AssertNotReleased();
    }

    // Once the consulted call is over, a missed release is put right like any other.
    [Fact]
    public async Task RecoverAsync_ABusyAgentWhoseConsultedCallIsOver_IsReturnedToWork()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFrom(new AgentStateChangedEventData { CurrentState = AgentPresenceStatus.Busy, Source = AgentStateChangeSources.ConsultAnswered, InteractionId = "colleague-call" });
        fixture.Interactions
            .Setup(manager => manager.FindByIdAsync("colleague-call", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "colleague-call", AgentId = "colleague", EndedUtc = _now.AddMinutes(-3) }.RestorePersistedStatus(InteractionStatus.Ended));

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
    }

    // Another live interaction of their own keeps the agent Busy, whatever became of the one that made them Busy.
    [Fact]
    public async Task RecoverAsync_ABusyAgentWithAnotherLiveInteraction_IsLeftAlone()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFromAcceptedCall(InteractionStatus.Ended, endedUtc: _now.AddMinutes(-5));
        fixture.Interactions
            .Setup(manager => manager.CountActiveByAgentAsync("busy-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        fixture.AssertNotReleased();
    }

    // With no history, or a history whose latest change is not into Busy, nothing says what the agent is doing.
    [Fact]
    public async Task RecoverAsync_ABusyAgentWithNoBusyChangeOnRecord_IsLeftAlone()
    {
        // Arrange
        var fixture = new BusyFixture();

        // Act
        var none = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        fixture.BusyFrom(new AgentStateChangedEventData { CurrentState = AgentPresenceStatus.Available, InteractionId = "interaction-1" });
        fixture.Interactions
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1", EndedUtc = _now.AddMinutes(-5) }.RestorePersistedStatus(InteractionStatus.Ended));
        var disagreeing = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, none);
        Assert.Equal(0, disagreeing);
        fixture.AssertNotReleased();
    }

    // The call's own end releases the agent within moments; recovery only picks up what that missed.
    [Fact]
    public async Task RecoverAsync_ACallThatEndedMomentsAgo_IsLeftToItsOwnRelease()
    {
        // Arrange
        var fixture = new BusyFixture();
        fixture.BusyFromAcceptedCall(InteractionStatus.Ended, endedUtc: _now.AddSeconds(-10));

        // Act
        var recovered = await fixture.Service.RecoverAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        fixture.AssertNotReleased();
    }

    private sealed class BusyFixture
    {
        private readonly List<InteractionEvent> _changes = [];

        public BusyFixture()
        {
            Agents
                .Setup(manager => manager.GetByPresenceAsync(AgentPresenceStatus.Busy, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AgentProfile { ItemId = "busy-agent", PresenceStatus = AgentPresenceStatus.Busy }]);
            Agents
                .Setup(manager => manager.GetByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Events
                .Setup(store => store.GetLatestBeforeAsync(
                    nameof(AgentProfile),
                    It.Is<IEnumerable<string>>(types => types.Contains(ContactCenterConstants.Events.AgentStateChanged)),
                    It.Is<IEnumerable<string>>(ids => ids.Contains("busy-agent")),
                    _now,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _changes);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Service = new AgentAvailabilityRecoveryService(
                Agents.Object,
                Interactions.Object,
                Presence.Object,
                Reservations.Object,
                Events.Object,
                Options.Create(new AgentAvailabilityOptions()),
                clock.Object,
                new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);
        }

        public Mock<IAgentProfileManager> Agents { get; } = new();

        public Mock<IInteractionManager> Interactions { get; } = new();

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public Mock<IActivityReservationManager> Reservations { get; } = new();

        public Mock<IInteractionEventStore> Events { get; } = new();

        public AgentAvailabilityRecoveryService Service { get; }

        public void BusyFrom(AgentStateChangedEventData change)
        {
            change.AgentId = "busy-agent";

            var interactionEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.AgentStateChanged,
                AggregateType = nameof(AgentProfile),
                AggregateId = "busy-agent",
                OccurredUtc = _now.AddMinutes(-30),
            };
            interactionEvent.SetData(change);

            _changes.Clear();
            _changes.Add(interactionEvent);
        }

        public void BusyFromAcceptedCall(InteractionStatus status, DateTime? endedUtc, bool namesInteraction = true)
        {
            BusyFrom(new AgentStateChangedEventData
            {
                CurrentState = AgentPresenceStatus.Busy,
                Source = AgentStateChangeSources.Accepted,
                ReservationId = "reservation-1",
                InteractionId = namesInteraction ? "interaction-1" : null,
            });

            var interaction = new Interaction { ItemId = "interaction-1", ActivityItemId = "activity-1", AgentId = "busy-agent", EndedUtc = endedUtc }
                .RestorePersistedStatus(status);

            Reservations
                .Setup(manager => manager.FindByIdAsync("reservation-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityReservation { ItemId = "reservation-1", ActivityItemId = "activity-1", AgentId = "busy-agent" });
            Interactions
                .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);
            Interactions
                .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);
        }

        public void AssertNotReleased()
            => Presence.Verify(
                manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    private static AgentAvailabilityRecoveryService CreateService(
        AgentProfile agent,
        Interaction interaction,
        Mock<IAgentPresenceManager> presenceManager,
        Mock<IInteractionManager> interactionManager = null)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetByPresenceAsync(AgentPresenceStatus.WrapUp, It.IsAny<CancellationToken>()))
            .ReturnsAsync([agent]);

        if (interactionManager is null)
        {
            interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.GetPendingWrapUpsByAgentAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction is null ? [] : [interaction]);
        }

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new AgentAvailabilityRecoveryService(
            agentManager.Object,
            interactionManager.Object,
            presenceManager.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IInteractionEventStore>().Object,
            Options.Create(new AgentAvailabilityOptions()),
            clock.Object,
            new Mock<ILogger<AgentAvailabilityRecoveryService>>().Object);
    }

    private static AgentProfile CreateAgent()
    {
        return new AgentProfile
        {
            ItemId = "a1",
            PresenceStatus = AgentPresenceStatus.WrapUp,
        };
    }

    private static Interaction CreateInteraction(DateTime wrapUpStartedUtc)
    {
        return new Interaction
        {
            ItemId = "i1",
            AgentId = "a1",
            WrapUpStartedUtc = wrapUpStartedUtc,
        };
    }
}
