using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class StaleAgentWorkServiceTests
{
    private static readonly DateTime _now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    // The provider says the call is gone: the soft-phone call record is stale, whatever else is true.
    [Fact]
    public async Task Detect_AnActiveCallTheProviderNoLongerHas_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 10);
        harness.ProviderSays(found: false);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        var item = Assert.Single(Assert.Single(stale.Values).Items);
        Assert.Equal(StaleAgentWorkKind.TelephonyCall, item.Kind);
        Assert.Equal("ti-1", item.ReferenceId);
    }

    // The case that left an agent "On a call" with nothing on the line: the lookup itself fails, every time.
    [Fact]
    public async Task Detect_AnActiveCallTheProviderCannotResolveForLongEnough_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 6);
        harness.ProviderCannotAnswer();

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Equal(StaleAgentWorkKind.TelephonyCall, Assert.Single(Assert.Single(stale.Values).Items).Kind);
    }

    [Fact]
    public async Task Detect_AnActiveCallTheProviderCannotResolveYet_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 3);
        harness.ProviderCannotAnswer();

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_ACallTheProviderConfirmsIsAlive_IsNeverStale_HoweverOldItIs()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 60 * 20);
        harness.ProviderSays(found: true, CallState.Connected);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_ACallTooNewToJudge_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 1);
        harness.ProviderSays(found: false);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_ABrowserPlacedCallWithNoLiveSessionThatWentSilent_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 30, providerName: null);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Equal(StaleAgentWorkKind.TelephonyCall, Assert.Single(Assert.Single(stale.Values).Items).Kind);
    }

    [Fact]
    public async Task Detect_ABrowserPlacedCallTheLiveSessionStillReports_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 30, providerName: null, lastReportedMinutesAgo: 0);
        harness.AddLiveSession();

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_AnInteractionTheProviderReportsGone_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddInteraction(startedMinutesAgo: 20);
        harness.ProviderSays(found: false);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        var item = Assert.Single(Assert.Single(stale.Values).Items);
        Assert.Equal(StaleAgentWorkKind.ProviderInteraction, item.Kind);
        Assert.Equal("int-1", item.ReferenceId);
    }

    [Fact]
    public async Task Detect_AnInteractionTheProviderCannotAnswerFor_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddInteraction(startedMinutesAgo: 20);
        harness.ProviderCannotAnswer();

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_ABusyAgentWithNothingBehindThemPastTheGracePeriod_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = AgentPresenceStatus.Busy;
        harness.Agent.PresenceChangedUtc = _now.AddMinutes(-5);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Equal(StaleAgentWorkKind.Presence, Assert.Single(Assert.Single(stale.Values).Items).Kind);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.Reserved)]
    public async Task Detect_ABusyAgentStillWithinTheGracePeriod_IsNotStale(AgentPresenceStatus status)
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = status;
        harness.Agent.PresenceChangedUtc = _now.AddSeconds(-20);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_AReservedAgentWithAnOfferRinging_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = AgentPresenceStatus.Reserved;
        harness.Agent.PresenceChangedUtc = _now.AddMinutes(-5);
        harness.PendingOffer = Offer(ReservationStatus.Pending, expiresUtc: _now.AddSeconds(10));

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_ABusyAgentWhoIsOnALiveCall_IsNotStale()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = AgentPresenceStatus.Busy;
        harness.Agent.PresenceChangedUtc = _now.AddMinutes(-30);
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: true, CallState.Connected);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Empty(stale);
    }

    [Fact]
    public async Task Detect_AReservationThatNothingBacks_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.ActiveReservationId = "res-1";
        harness.Reservations["res-1"] = Offer(ReservationStatus.Expired, expiresUtc: _now.AddMinutes(-10));

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        var item = Assert.Single(Assert.Single(stale.Values).Items);
        Assert.Equal(StaleAgentWorkKind.Reservation, item.Kind);
        Assert.Equal("res-1", item.ReferenceId);
    }

    [Fact]
    public async Task Detect_AnAssignedQueueItemWithNoCall_IsStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueueItem(QueueItemStatus.Assigned, modifiedMinutesAgo: 10);

        // Act
        var stale = await harness.DetectAsync();

        // Assert
        Assert.Equal(StaleAgentWorkKind.QueueItem, Assert.Single(Assert.Single(stale.Values).Items).Kind);
    }

    // A provider's answer is reused between polls so the dashboard does not hit it on every refresh.
    [Fact]
    public async Task Detect_ReusesTheProvidersAnswerBetweenPolls()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 10);
        harness.ProviderSays(found: false);

        // Act
        await harness.DetectAsync();
        await harness.DetectAsync();

        // Assert
        harness.StateProvider.Verify(
            provider => provider.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Release_SettlesTheStaleCall_ReleasesTheirWork_AndReturnsThemToReady()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = AgentPresenceStatus.Busy;
        harness.Agent.PresenceChangedUtc = _now.AddMinutes(-30);
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: false);
        harness.AddQueueItem(QueueItemStatus.Assigned, modifiedMinutesAgo: 30);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.True(release.Released, release.Reason);
        var call = Assert.Single(harness.Calls);
        Assert.Equal(CallOutcome.Failed, call.Outcome);
        Assert.Equal(_now, call.EndedUtc);
        Assert.Equal(1800, call.DurationSeconds);
        harness.Healing.Verify(service => service.HealForResetAsync("agent-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(QueueItemStatus.Waiting, harness.QueueItems.Single().Status);
        Assert.Null(harness.QueueItems.Single().AgentId);
        Assert.Equal(AgentPresenceStatus.Available, release.ReturnedTo);
        harness.Presence.Verify(manager => manager.SetPresenceAsync(
            "user-1",
            AgentPresenceStatus.Available,
            null,
            It.Is<AgentStateChangeContext>(context =>
                context.Actor == ContactCenterActor.Supervisor("sup-1") &&
                context.Source == AgentStateChangeSources.StaleWorkReleased),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Release_AnAssignedItemWhoseCallIsOver_IsCompletedRatherThanRequeued()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: false);
        harness.AddQueueItem(QueueItemStatus.Assigned, modifiedMinutesAgo: 30);
        harness.EndedInteractionForActivity = new Interaction { ItemId = "done", ActivityItemId = "act-1" }.RestorePersistedStatus(InteractionStatus.Ended);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.True(release.Released, release.Reason);
        Assert.Equal(QueueItemStatus.Completed, harness.QueueItems.Single().Status);
    }

    [Fact]
    public async Task Release_SettlesAnInteractionTheProviderReportsGone_FromProviderTruth()
    {
        // Arrange
        var harness = new Harness();
        harness.AddInteraction(startedMinutesAgo: 20);
        harness.ProviderSays(found: false);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.True(release.Released, release.Reason);
        harness.Synchronization.Verify(service => service.RefreshInteractionAsync(
            It.Is<Interaction>(interaction => interaction.ItemId == "int-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Release_WhenTheSynchronizationCannotSettleTheInteraction_LeavesTheRestOfTheAgentsWork()
    {
        // Arrange
        var harness = new Harness { SynchronizationSettles = false };
        harness.AddInteraction(startedMinutesAgo: 20);
        harness.ProviderSays(found: false);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.False(release.Released);
        harness.Healing.Verify(service => service.HealForResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Presence.VerifyNoOtherCalls();
    }

    // The core safety rule: a call the provider confirms is alive is never ended, and nothing else of the agent's is
    // released while it is up.
    [Fact]
    public async Task Release_WhenTheProviderConfirmsACallIsAlive_DeclinesAndTouchesNothing()
    {
        // Arrange
        var harness = new Harness();
        harness.Agent.PresenceStatus = AgentPresenceStatus.Busy;
        harness.Agent.PresenceChangedUtc = _now.AddMinutes(-30);
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: true, CallState.Connected);
        harness.AddQueueItem(QueueItemStatus.Assigned, modifiedMinutesAgo: 30);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.False(release.Released);
        Assert.True(release.DeclinedForLiveCall);
        Assert.Contains("provider confirms", release.Reason);
        Assert.Equal(CallOutcome.InProgress, Assert.Single(harness.Calls).Outcome);
        Assert.Null(harness.Calls[0].EndedUtc);
        Assert.Equal(QueueItemStatus.Assigned, harness.QueueItems.Single().Status);
        harness.TelephonyStore.Verify(store => store.UpdateByIdAsync(It.IsAny<string>(), It.IsAny<Func<TelephonyInteraction, bool>>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Healing.Verify(service => service.HealForResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Presence.VerifyNoOtherCalls();
    }

    // The dashboard may be showing an answer from half a minute ago; releasing asks the provider again.
    [Fact]
    public async Task Release_AsksTheProviderAgain_WhenTheDashboardsAnswerHasGoneStale()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: false);
        var shown = await harness.DetectAsync();
        Assert.NotEmpty(shown);
        harness.ProviderSays(found: true, CallState.Connected);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.False(release.Released);
        Assert.True(release.DeclinedForLiveCall);
        Assert.Equal(CallOutcome.InProgress, Assert.Single(harness.Calls).Outcome);
    }

    // A call the platform cannot show to be over (the provider cannot answer, and it is young) is not freed either.
    [Fact]
    public async Task Release_WhenACallCannotBeShownToBeOver_Declines()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 3);
        harness.ProviderCannotAnswer();
        harness.AddQueueItem(QueueItemStatus.Assigned, modifiedMinutesAgo: 30);

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.False(release.Released);
        Assert.True(release.DeclinedForLiveCall);
        Assert.Equal(CallOutcome.InProgress, Assert.Single(harness.Calls).Outcome);
    }

    [Fact]
    public async Task Release_WithNothingStale_SaysSo()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var release = await harness.ReleaseAsync();

        // Assert
        Assert.False(release.Released);
        Assert.False(release.DeclinedForLiveCall);
        Assert.Contains("nothing stale", release.Reason);
    }

    // A call another path already settled while the supervisor was deciding is left as it ended.
    [Fact]
    public async Task Release_DoesNotOverwriteACallThatWasSettledInTheMeantime()
    {
        // Arrange
        var harness = new Harness();
        harness.AddCall(startedMinutesAgo: 30);
        harness.ProviderSays(found: false);
        harness.OnBeforeSettle = call =>
        {
            call.Outcome = CallOutcome.Completed;
            call.EndedUtc = _now.AddMinutes(-1);
        };

        // Act
        await harness.ReleaseAsync();

        // Assert
        Assert.Equal(CallOutcome.Completed, harness.Calls.Single().Outcome);
        Assert.Equal(_now.AddMinutes(-1), harness.Calls.Single().EndedUtc);
    }

    private static ActivityReservation Offer(ReservationStatus status, DateTime expiresUtc)
    {
        var reservation = new ActivityReservation
        {
            ItemId = "res-1",
            AgentId = "agent-1",
            QueueItemId = "qi-1",
            CreatedUtc = expiresUtc.AddMinutes(-1),
            ExpiresUtc = expiresUtc,
        }.RestorePersistedStatus(status);

        return reservation;
    }

    private sealed class Harness
    {
        private readonly List<AgentSession> _sessions = [];
        private readonly List<Interaction> _interactions = [];

        public Harness()
        {
            Agent = new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["queue-1"],
            };

            var sessions = new Mock<IAgentSessionManager>();
            sessions
                .Setup(manager => manager.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _sessions.ToArray());

            var interactions = new Mock<IInteractionManager>();
            interactions
                .Setup(manager => manager.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _interactions.ToArray());
            interactions
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => _interactions.FirstOrDefault(interaction => interaction.ItemId == id));
            interactions
                .Setup(manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => EndedInteractionForActivity);

            var queueItems = new Mock<IQueueItemManager>();
            queueItems
                .Setup(manager => manager.GetOpenByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => QueueItems
                    .Where(item => item.Status is QueueItemStatus.Reserved or QueueItemStatus.Assigned)
                    .ToArray());
            queueItems
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => QueueItems.FirstOrDefault(item => item.ItemId == id));

            var agents = new Mock<IAgentProfileManager>();
            agents
                .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Agent);

            Presence
                .Setup(manager => manager.SetPresenceAsync(It.IsAny<string>(), It.IsAny<AgentPresenceStatus>(), It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, AgentPresenceStatus status, string _, AgentStateChangeContext _, CancellationToken _) =>
                {
                    Agent.PresenceStatus = status;

                    return Agent;
                });

            Synchronization
                .Setup(service => service.RefreshInteractionAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Interaction interaction, CancellationToken _) =>
                {
                    if (SynchronizationSettles)
                    {
                        interaction.RestorePersistedStatus(InteractionStatus.Ended);
                    }

                    return interaction;
                });

            var reservations = new Mock<IActivityReservationManager>();
            reservations
                .Setup(manager => manager.FindPendingByAgentAsync("agent-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => PendingOffer);
            reservations
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => Reservations.GetValueOrDefault(id));

            TelephonyStore
                .Setup(store => store.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Calls.Where(call => call.Outcome == CallOutcome.InProgress && !call.EndedUtc.HasValue).ToArray());
            TelephonyStore
                .Setup(store => store.UpdateByIdAsync(It.IsAny<string>(), It.IsAny<Func<TelephonyInteraction, bool>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, Func<TelephonyInteraction, bool> mutate, CancellationToken _) =>
                {
                    var call = Calls.FirstOrDefault(candidate => candidate.InteractionId == id);

                    if (call is not null)
                    {
                        OnBeforeSettle?.Invoke(call);
                        mutate(call);
                    }

                    return call;
                });

            var provider = new Mock<ITelephonyProvider>();
            StateProvider = provider.As<ITelephonyCallStateProvider>();

            var resolver = new Mock<ITelephonyProviderResolver>();
            resolver.Setup(value => value.GetAsync(It.IsAny<string>())).ReturnsAsync(provider.Object);

            Service = new StaleAgentWorkService(
                sessions.Object,
                interactions.Object,
                queueItems.Object,
                agents.Object,
                Presence.Object,
                Healing.Object,
                [Synchronization.Object],
                Mock.Of<IContactCenterWorkStateService>(),
                [reservations.Object],
                [TelephonyStore.Object],
                [resolver.Object],
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(new AgentAvailabilityOptions()),
                Options.Create(new StaleAgentWorkOptions()),
                new StubClock(_now),
                NullLogger<StaleAgentWorkService>.Instance);

            QueueItemManager = queueItems;
        }

        public AgentProfile Agent { get; }

        public StaleAgentWorkService Service { get; }

        public List<TelephonyInteraction> Calls { get; } = [];

        public List<QueueItem> QueueItems { get; } = [];

        public Dictionary<string, ActivityReservation> Reservations { get; } = new(StringComparer.Ordinal);

        public ActivityReservation PendingOffer { get; set; }

        public Interaction EndedInteractionForActivity { get; set; }

        public bool SynchronizationSettles { get; set; } = true;

        public Action<TelephonyInteraction> OnBeforeSettle { get; set; }

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public Mock<IAgentWorkStateHealingService> Healing { get; } = new();

        public Mock<IProviderCallStateSynchronizationService> Synchronization { get; } = new();

        public Mock<ITelephonyInteractionStore> TelephonyStore { get; } = new();

        public Mock<ITelephonyCallStateProvider> StateProvider { get; }

        public Mock<IQueueItemManager> QueueItemManager { get; }

        public void AddCall(int startedMinutesAgo, string providerName = "provider-1", int? lastReportedMinutesAgo = null)
        {
            var call = new TelephonyInteraction
            {
                InteractionId = "ti-1",
                CallId = "call-1",
                ProviderName = providerName,
                UserId = "user-1",
                Outcome = CallOutcome.InProgress,
                StartedUtc = _now.AddMinutes(-startedMinutesAgo),
            };

            if (lastReportedMinutesAgo.HasValue)
            {
                call.Put(new ClientRecordedCallActivity { LastReportedUtc = _now.AddMinutes(-lastReportedMinutesAgo.Value) });
            }

            Calls.Add(call);
        }

        public void AddInteraction(int startedMinutesAgo)
        {
            var interaction = new Interaction
            {
                ItemId = "int-1",
                AgentId = "agent-1",
                ProviderName = "provider-1",
                ProviderInteractionId = "call-1",
                StartedUtc = _now.AddMinutes(-startedMinutesAgo),
                CreatedUtc = _now.AddMinutes(-startedMinutesAgo),
            }.RestorePersistedStatus(InteractionStatus.Connected);

            _interactions.Add(interaction);
        }

        public void AddLiveSession()
            => _sessions.Add(new AgentSession
            {
                UserId = "user-1",
                IsOnline = true,
                ConnectionIds = ["connection-1"],
                LastHeartbeatUtc = _now.AddSeconds(-10),
            });

        public void AddQueueItem(QueueItemStatus status, int modifiedMinutesAgo)
            => QueueItems.Add(new QueueItem
            {
                ItemId = "qi-1",
                QueueId = "queue-1",
                ActivityItemId = "act-1",
                AgentId = "agent-1",
                ReservationId = "res-1",
                QueueEnteredUtc = _now.AddMinutes(-modifiedMinutesAgo),
                ModifiedUtc = _now.AddMinutes(-modifiedMinutesAgo),
            }.RestorePersistedStatus(status));

        public void ProviderSays(bool found, CallState state = CallState.Disconnected)
            => StateProvider
                .Setup(provider => provider.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TelephonyCallLookupResult
                {
                    Succeeded = true,
                    Found = found,
                    Call = found ? new TelephonyCall { CallId = "call-1", State = state } : null,
                });

        public void ProviderCannotAnswer()
            => StateProvider
                .Setup(provider => provider.GetCallStateAsync("call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TelephonyCallLookupResult { Succeeded = false, Error = "The provider rejected the lookup." });

        public Task<IReadOnlyDictionary<string, StaleAgentWork>> DetectAsync()
            => Service.DetectAsync([Agent], TestContext.Current.CancellationToken);

        public Task<StaleAgentWorkRelease> ReleaseAsync()
            => Service.ReleaseAsync(Agent, ContactCenterActor.Supervisor("sup-1"), TestContext.Current.CancellationToken);
    }
}
