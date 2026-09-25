using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A caller whose offer an agent turns down, or lets ring out, goes to the next agent in line rather than back to the
/// one who did not take them. Live, a queue with two Available agents offered one caller to the same agent five times
/// in a row -- two declines, a missed offer, two more declines -- and never once to the other: the agent who declined
/// was still the longest idle, so routing picked them again every time. Runs the real queue, reservation, routing and
/// assignment services, and the decline command, over the harness's SQLite store.
/// </summary>
public sealed class DeclinedOfferRoutingTests
{
    private const string AgentA = "agent-1";
    private const string UserA = "user-1";
    private const string AgentB = "agent-2";
    private const string UserB = "user-2";

    [Fact]
    public async Task ADeclinedOffer_GoesToTheNextAvailableAgent_NotBackToTheAgentWhoDeclinedIt()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync();
        var first = await context.OfferNextAsync();

        // Act
        var declined = await context.DeclineAsync(first, UserA);
        var second = await context.OfferNextAsync();

        // Assert
        Assert.Equal(AgentA, first.AgentId);
        Assert.True(declined.Succeeded, declined.Reason);
        Assert.NotNull(second);
        Assert.Equal(AgentB, second.AgentId);
        Assert.Equal([AgentA], (await context.FindItemAsync()).DeclinedAgentIds);
    }

    [Fact]
    public async Task AnOfferThatRingsOut_GoesToTheNextAvailableAgent()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync();
        var first = await context.OfferNextAsync();

        // Act
        context.Harness.Clock.Advance(TimeSpan.FromSeconds(DeclineContext.RingSeconds + 1));
        await context.ReservationService.ExpireDueAsync(TestContext.Current.CancellationToken);
        await context.Harness.CommitAsync();
        var second = await context.OfferNextAsync();

        // Assert
        Assert.Equal(AgentA, first.AgentId);
        Assert.Equal(ReservationStatus.Expired, (await context.FindReservationAsync(first.ItemId)).Status);
        Assert.Equal(AgentB, second?.AgentId);
    }

    [Fact]
    public async Task TheStickyAgent_WhoDeclines_IsNotPreferredAgain()
    {
        // Arrange
        // B is who the customer last spoke to, so B is offered the call first although A is ahead in line.
        await using var context = await DeclineContext.CreateAsync(preferStickyAgent: true, stickyAgentUserId: UserB);
        var first = await context.OfferNextAsync();

        // Act
        await context.DeclineAsync(first, UserB);
        var second = await context.OfferNextAsync();

        // Assert
        Assert.Equal(AgentB, first.AgentId);
        Assert.Equal(AgentA, second?.AgentId);
    }

    [Fact]
    public async Task WhenEveryAgentHasDeclined_TheNextRoundStartsWithTheFirstToDecline_NeverTheOneWhoJustDid()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync();
        await context.DeclineAsync(await context.OfferNextAsync(), UserA);
        await context.DeclineAsync(await context.OfferNextAsync(), UserB);

        // Act
        var third = await context.OfferNextAsync();

        // Assert
        Assert.Equal(AgentA, third?.AgentId);
        Assert.Equal([AgentA, AgentB], (await context.FindItemAsync()).DeclinedAgentIds);
    }

    [Fact]
    public async Task TheOnlyAvailableAgent_WhoDeclines_IsNotRungStraightBack_AndTheCallerKeepsWaiting()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync(signInB: false);
        var first = await context.OfferNextAsync();

        // Act
        await context.DeclineAsync(first, UserA);
        var straightAfter = await context.OfferNextAsync();

        context.Harness.Clock.Advance(ActivityRoutingService.DeclinedOfferRetryDelay);
        var afterTheRetryDelay = await context.OfferNextAsync();

        // Assert
        // In between, the queue's own no-agent handling applies: the caller waits with its treatment, and its maximum
        // wait or overflow still moves them on.
        Assert.Null(straightAfter);
        Assert.Equal(AgentA, afterTheRetryDelay?.AgentId);
    }

    [Fact]
    public async Task DecliningTheSameOfferTwice_DoesNothingTheSecondTime()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync();
        var first = await context.OfferNextAsync();
        await context.DeclineAsync(first, UserA);
        var second = await context.OfferNextAsync();

        // Act
        // A second client (or a second click) names the offer that has already been turned down.
        var repeated = await context.DeclineAsync(first, UserA);

        // Assert
        Assert.False(repeated.Succeeded);
        Assert.Equal(ReservationStatus.Pending, (await context.FindReservationAsync(second.ItemId)).Status);
        Assert.Equal(AgentPresenceStatus.Reserved, await context.Harness.GetPresenceAsync(AgentB));
        Assert.Single(context.Harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.OfferDeclined && e.AggregateId == first.ItemId);
        Assert.Equal([AgentA], (await context.FindItemAsync()).DeclinedAgentIds);
    }

    [Fact]
    public async Task DecliningAnotherAgentsOffer_IsRefused()
    {
        // Arrange
        await using var context = await DeclineContext.CreateAsync();
        var first = await context.OfferNextAsync();

        // Act
        var declined = await context.DeclineAsync(first, UserB);

        // Assert
        Assert.False(declined.Succeeded);
        Assert.Equal(ReservationStatus.Pending, (await context.FindReservationAsync(first.ItemId)).Status);
        Assert.Empty((await context.FindItemAsync()).DeclinedAgentIds);
    }

    /// <summary>
    /// One caller waiting in a real (persisted) queue routed longest-idle, with A ahead of B in line.
    /// </summary>
    private sealed class DeclineContext : IAsyncDisposable
    {
        public const int RingSeconds = 30;

        private string _queueItemId;

        private DeclineContext(DialerModeIntegrationHarness harness)
        {
            Harness = harness;
        }

        public DialerModeIntegrationHarness Harness { get; }

        public ActivityReservationService ReservationService { get; private set; }

        public ActivityAssignmentService AssignmentService { get; private set; }

        public ContactCenterCallCommandService CallCommands { get; private set; }

        public static async Task<DeclineContext> CreateAsync(bool preferStickyAgent = false, string stickyAgentUserId = null, bool signInB = true)
        {
            var harness = await DialerModeIntegrationHarness.CreateAsync();
            var context = new DeclineContext(harness);
            var queue = new ActivityQueue
            {
                ItemId = DialerModeIntegrationHarness.QueueId,
                Name = "Support",
                Enabled = true,
                RoutingStrategy = QueueRoutingStrategy.LongestIdle,
                PreferStickyAgent = preferStickyAgent,
                ReservationTimeoutSeconds = RingSeconds,
                UnansweredOfferAction = UnansweredOfferAction.Requeue,
            };

            context.Compose(queue);

            // Neither has an idle time recorded, so longest-idle routing keeps the order they signed in: A first.
            await harness.SignInAgentAsync(AgentA, UserA);

            if (signInB)
            {
                await harness.SignInAgentAsync(AgentB, UserB);
            }

            var item = await harness.SeedQueuedActivityAsync(
                new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Pending },
                DialerModeIntegrationHarness.QueueId);

            if (stickyAgentUserId is not null)
            {
                var stored = await harness.Services.GetRequiredService<IQueueItemManager>().FindByIdAsync(item.ItemId, TestContext.Current.CancellationToken);
                stored.StickyAgentUserId = stickyAgentUserId;
                await harness.Services.GetRequiredService<IQueueItemManager>().UpdateAsync(stored, cancellationToken: TestContext.Current.CancellationToken);
                await harness.CommitAsync();
            }

            context._queueItemId = item.ItemId;

            return context;
        }

        public async Task<ActivityReservation> OfferNextAsync()
        {
            var reservation = await AssignmentService.AssignNextAsync(DialerModeIntegrationHarness.QueueId, TestContext.Current.CancellationToken);
            await Harness.CommitAsync();

            return reservation;
        }

        public async Task<CallCommandResult> DeclineAsync(ActivityReservation reservation, string userId)
        {
            Assert.NotNull(reservation);

            var result = await CallCommands.DeclineInboundOfferAsync(reservation.ItemId, userId, TestContext.Current.CancellationToken);
            await Harness.CommitAsync();

            return result;
        }

        public async Task<QueueItem> FindItemAsync()
            => await Harness.Services.GetRequiredService<IQueueItemManager>().FindByIdAsync(_queueItemId, TestContext.Current.CancellationToken);

        public async Task<ActivityReservation> FindReservationAsync(string reservationId)
            => await Harness.Services.GetRequiredService<IActivityReservationManager>().FindByIdAsync(reservationId, TestContext.Current.CancellationToken);

        public ValueTask DisposeAsync() => Harness.DisposeAsync();

        private void Compose(ActivityQueue queue)
        {
            var services = Harness.Services;

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(id == queue.ItemId ? queue : null));

            var businessHours = new Mock<IBusinessHoursService>();
            businessHours
                .Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var availability = new HarnessAvailabilityService(Harness.AgentManager);
            var queueService = ActivatorUtilities.CreateInstance<ActivityQueueService>(services, queueManager.Object, businessHours.Object, Mock.Of<IQueueTreatmentProvider>());
            var reservationService = ActivatorUtilities.CreateInstance<ActivityReservationService>(services, queueManager.Object, (IActivityQueueService)queueService, (IAgentAvailabilityService)availability);
            var withdrawalService = ActivatorUtilities.CreateInstance<QueuedWorkWithdrawalService>(services, (IActivityQueueService)queueService, (IActivityReservationService)reservationService);
            var routingService = new ActivityRoutingService(
            [
                new CapacityRoutingStrategy(),
                new StickyAgentRoutingStrategy(),
                new LongestIdleRoutingStrategy(),
            ], services.GetRequiredService<IClock>());

            ReservationService = reservationService;
            AssignmentService = ActivatorUtilities.CreateInstance<ActivityAssignmentService>(
                services,
                (IActivityQueueManager)queueManager.Object,
                (IActivityReservationService)reservationService,
                (IQueuedWorkWithdrawalService)withdrawalService,
                (IActivityRoutingService)routingService,
                businessHours.Object,
                new SignedInAgents(Harness, availability));

            CallCommands = ActivatorUtilities.CreateInstance<ContactCenterCallCommandService>(
                services,
                (IActivityReservationService)reservationService,
                Mock.Of<IDialerProfileReader>(),
                Enumerable.Empty<IDialerAttemptService>(),
                Mock.Of<IContactCenterVoiceProviderResolver>(),
                (IActivityQueueService)queueService,
                Enumerable.Empty<IContactCenterOfferAnsweredNotifier>(),
                Mock.Of<IAgentPreDialCoordinator>());
        }
    }

    /// <summary>
    /// Every signed-in agent who is Available with no offer ringing, for the queue.
    /// </summary>
    private sealed class SignedInAgents : IAgentAvailabilityService
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly HarnessAvailabilityService _inner;

        public SignedInAgents(DialerModeIntegrationHarness harness, HarnessAvailabilityService inner)
        {
            _harness = harness;
            _inner = inner;
        }

        public Task<AgentAvailability> GetAsync(string agentId, string queueId, CancellationToken cancellationToken = default)
            => _inner.GetAsync(agentId, queueId, cancellationToken);

        public Task<AgentAvailability> GetForDirectAsync(string agentId, CancellationToken cancellationToken = default)
            => _inner.GetForDirectAsync(agentId, cancellationToken);

        public async Task<IReadOnlyCollection<AgentAvailability>> GetForQueueAsync(string queueId, CancellationToken cancellationToken = default)
        {
            var available = new List<AgentAvailability>();

            foreach (var agentId in _harness.AgentIds)
            {
                if (await _inner.GetAsync(agentId, queueId, cancellationToken) is { } availability)
                {
                    available.Add(availability);
                }
            }

            return available;
        }
    }
}
