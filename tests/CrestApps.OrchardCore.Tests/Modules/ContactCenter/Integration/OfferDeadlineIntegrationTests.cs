using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// An offer nobody answers is settled at its deadline, not at the next background sweep. Live, a thirty-second offer
/// rang for seventy-three: Orchard runs a tenant's background tasks one after another and the expiry sweep only came
/// round again after the other tasks had held the loop, so the caller kept ringing and the agent stayed Reserved long
/// after both screens had stopped. Runs the real reservation service over the harness's SQLite store, with time
/// driven by the test and no sweep ever run.
/// </summary>
public sealed class OfferDeadlineIntegrationTests
{
    private const int ReservationTimeoutSeconds = 30;

    [Fact]
    public async Task AnUnansweredOffer_ExpiresAtItsDeadline_WithoutTheSweep()
    {
        // Arrange
        await using var context = await DeadlineContext.CreateAsync();
        var reservation = await context.OfferAsync();

        // Act: just short of the deadline, then past it.
        await context.AdvanceAsync(TimeSpan.FromSeconds(ReservationTimeoutSeconds) - TimeSpan.FromMilliseconds(500));
        var beforeDeadline = (await context.FindReservationAsync(reservation.ItemId)).Status;

        await context.AdvanceAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(ReservationStatus.Pending, beforeDeadline);

        var settled = await context.FindReservationAsync(reservation.ItemId);
        Assert.Equal(ReservationStatus.Expired, settled.Status);
        Assert.InRange(settled.ModifiedUtc.Value - reservation.ExpiresUtc, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.Equal(AgentPresenceStatus.Available, await context.Fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Equal(QueueItemStatus.Waiting, (await context.Fixture.FindQueueItemAsync(reservation.QueueItemId)).Status);
        Assert.Single(context.Released(reservation.ItemId));
        Assert.Equal(0, context.Scheduler.Count);
    }

    [Fact]
    public async Task AnAcceptJustBeforeTheDeadline_WinsAndTheDeadlineDoesNothing()
    {
        // Arrange
        await using var context = await DeadlineContext.CreateAsync();
        var reservation = await context.OfferAsync();
        await context.AdvanceAsync(TimeSpan.FromSeconds(ReservationTimeoutSeconds) - TimeSpan.FromMilliseconds(100));

        // Act: the agent accepts, and the deadline is not told (a lost or late cancel), so it still fires.
        var accepted = await context.Fixture.ReservationService.AcceptAsync(reservation.ItemId, TestContext.Current.CancellationToken);
        await context.Fixture.Harness.CommitAsync();
        await context.AdvanceAsync(TimeSpan.FromSeconds(5));

        // Assert: exactly one outcome, the accept.
        Assert.NotNull(accepted);
        Assert.Equal(ReservationStatus.Accepted, (await context.FindReservationAsync(reservation.ItemId)).Status);
        Assert.Equal(AgentPresenceStatus.Busy, await context.Fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Single(context.Assigned(reservation.ItemId));
        Assert.Empty(context.Released(reservation.ItemId));
    }

    [Fact]
    public async Task AnAcceptJustAfterTheDeadlineFired_IsRefusedAndTheExpiryStands()
    {
        // Arrange
        await using var context = await DeadlineContext.CreateAsync();
        var reservation = await context.OfferAsync();
        await context.AdvanceAsync(TimeSpan.FromSeconds(ReservationTimeoutSeconds) + TimeSpan.FromMilliseconds(100));

        // Act
        var accepted = await context.Fixture.ReservationService.AcceptAsync(reservation.ItemId, TestContext.Current.CancellationToken);
        await context.Fixture.Harness.CommitAsync();

        // Assert: exactly one outcome, the expiry.
        Assert.Null(accepted);
        Assert.Equal(ReservationStatus.Expired, (await context.FindReservationAsync(reservation.ItemId)).Status);
        Assert.Equal(AgentPresenceStatus.Available, await context.Fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Single(context.Released(reservation.ItemId));
        Assert.Empty(context.Assigned(reservation.ItemId));
    }

    [Fact]
    public async Task AnAcceptedOffer_DropsItsDeadline()
    {
        // Arrange
        await using var context = await DeadlineContext.CreateAsync();
        var reservation = await context.OfferAsync();

        // Act
        await context.Fixture.ReservationService.AcceptAsync(reservation.ItemId, TestContext.Current.CancellationToken);
        await context.Fixture.Harness.CommitAsync();
        await context.DispatchAsync(ContactCenterConstants.Events.QueueItemAssigned, reservation.ItemId);

        // Assert
        Assert.Equal(0, context.Scheduler.Count);
        Assert.Equal(0, context.Time.PendingTimers);
    }

    [Fact]
    public async Task AnExtendedOffer_IsExpiredAtItsNewDeadline()
    {
        // Arrange
        await using var context = await DeadlineContext.CreateAsync();
        var reservation = await context.OfferAsync();
        var stored = await context.FindReservationAsync(reservation.ItemId);
        stored.ExpiresUtc = stored.ExpiresUtc.AddSeconds(20);
        await context.Fixture.Reservations.UpdateAsync(stored, cancellationToken: TestContext.Current.CancellationToken);
        await context.Fixture.Harness.CommitAsync();

        // Act: the original deadline passes, then the extended one.
        await context.AdvanceAsync(TimeSpan.FromSeconds(ReservationTimeoutSeconds + 1));
        var atOriginalDeadline = (await context.FindReservationAsync(reservation.ItemId)).Status;

        await context.AdvanceAsync(TimeSpan.FromSeconds(20));

        // Assert
        Assert.Equal(ReservationStatus.Pending, atOriginalDeadline);
        Assert.Equal(ReservationStatus.Expired, (await context.FindReservationAsync(reservation.ItemId)).Status);
    }

    /// <summary>
    /// The withdrawal fixture's real routing services, a deadline scheduler on test-driven time whose work runs
    /// against them and commits as a shell scope would, and the handler that arms and drops offer deadlines.
    /// </summary>
    private sealed class DeadlineContext : IAsyncDisposable
    {
        private DeadlineContext(QueuedWorkWithdrawalFixture fixture, ManualTimeProvider time, ContactCenterDeadlineScheduler scheduler)
        {
            Fixture = fixture;
            Time = time;
            Scheduler = scheduler;
            Handler = new OfferDeadlineEventHandler(scheduler, fixture.Reservations);
        }

        public QueuedWorkWithdrawalFixture Fixture { get; }

        public ManualTimeProvider Time { get; }

        public ContactCenterDeadlineScheduler Scheduler { get; }

        public OfferDeadlineEventHandler Handler { get; }

        public static async Task<DeadlineContext> CreateAsync()
        {
            var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
            var time = new ManualTimeProvider(fixture.Harness.Clock);
            var services = new ScopeServices(fixture);
            var scheduler = new ContactCenterDeadlineScheduler(
                fixture.Harness.Clock,
                time,
                async work =>
                {
                    await work(services);
                    await fixture.Harness.CommitAsync();
                },
                NullLogger.Instance);

            return new DeadlineContext(fixture, time, scheduler);
        }

        /// <summary>
        /// Offers the one waiting item to the one agent for thirty seconds, then delivers the reservation event to
        /// the deadline handler as the outbox would once the offer commits.
        /// </summary>
        public async Task<ActivityReservation> OfferAsync()
        {
            var agent = await Fixture.Harness.SignInAgentAsync("agent-1", "user-1");
            var (_, queueItem) = await Fixture.SeedAsync("activity-1", ActivityStatus.Pending);

            var reservation = await Fixture.ReservationService.ReserveAsync(queueItem, agent, ReservationTimeoutSeconds, TestContext.Current.CancellationToken);
            await Fixture.Harness.CommitAsync();

            Assert.NotNull(reservation);

            await DispatchAsync(ContactCenterConstants.Events.AgentReserved, reservation.ItemId);

            return reservation;
        }

        public Task AdvanceAsync(TimeSpan by) => Time.AdvanceAsync(by, Scheduler.WhenIdleAsync);

        public Task DispatchAsync(string eventType, string reservationId)
            => Handler.HandleAsync(new InteractionEvent
            {
                EventType = eventType,
                AggregateType = nameof(ActivityReservation),
                AggregateId = reservationId,
            }, TestContext.Current.CancellationToken);

        public async Task<ActivityReservation> FindReservationAsync(string reservationId)
            => await Fixture.Reservations.FindByIdAsync(reservationId, TestContext.Current.CancellationToken);

        public List<InteractionEvent> Released(string reservationId) => Events(ContactCenterConstants.Events.AgentReleased, reservationId);

        public List<InteractionEvent> Assigned(string reservationId) => Events(ContactCenterConstants.Events.QueueItemAssigned, reservationId);

        public async ValueTask DisposeAsync()
        {
            Scheduler.Dispose();
            await Fixture.DisposeAsync();
        }

        private List<InteractionEvent> Events(string eventType, string reservationId)
            => [.. Fixture.Harness.PublishedEvents.Where(e => e.EventType == eventType && e.AggregateId == reservationId)];
    }

    /// <summary>
    /// What a deadline's shell scope resolves: the fixture's reservation service as the expirer, and the harness for
    /// everything else.
    /// </summary>
    private sealed class ScopeServices : IServiceProvider
    {
        private readonly QueuedWorkWithdrawalFixture _fixture;

        public ScopeServices(QueuedWorkWithdrawalFixture fixture)
        {
            _fixture = fixture;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(IReservationDeadlineExpirer)
                ? (IReservationDeadlineExpirer)_fixture.ReservationService
                : _fixture.Harness.Services.GetService(serviceType);
    }
}
