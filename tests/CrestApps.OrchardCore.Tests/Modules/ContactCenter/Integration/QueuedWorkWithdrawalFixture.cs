using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Composes the real routing, queue, reservation and withdrawal services over the dialer harness's SQLite store,
/// so a test can drive queued work leaving its queue exactly as production does. Only the queue lookup, business
/// hours and hold-music provider are stood in, none of which a campaign queue uses.
/// </summary>
internal sealed class QueuedWorkWithdrawalFixture : IAsyncDisposable
{
    public static readonly string CampaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(DialerModeIntegrationHarness.CampaignId);

    private QueuedWorkWithdrawalFixture(
        DialerModeIntegrationHarness harness,
        IActivityReservationService reservationService,
        IQueuedWorkWithdrawalService withdrawalService,
        IActivityAssignmentService assignmentService)
    {
        Harness = harness;
        ReservationService = reservationService;
        WithdrawalService = withdrawalService;
        AssignmentService = assignmentService;
    }

    public DialerModeIntegrationHarness Harness { get; }

    public IActivityReservationService ReservationService { get; }

    public IQueuedWorkWithdrawalService WithdrawalService { get; }

    public IActivityAssignmentService AssignmentService { get; }

    public IQueueItemManager QueueItems => Harness.Services.GetRequiredService<IQueueItemManager>();

    public IActivityReservationManager Reservations => Harness.Services.GetRequiredService<IActivityReservationManager>();

    public IReadOnlyList<InteractionEvent> Withdrawals
        => Harness.PublishedEvents.Where(e => e.EventType == ContactCenterConstants.Events.QueueItemWithdrawn).ToList();

    public static async Task<QueuedWorkWithdrawalFixture> CreateAsync()
    {
        var harness = await DialerModeIntegrationHarness.CreateAsync();
        var services = harness.Services;

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours
            .Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        IActivityQueueService queueService = ActivatorUtilities.CreateInstance<ActivityQueueService>(
            services,
            businessHours.Object,
            Mock.Of<IQueueTreatmentProvider>());

        IActivityReservationService reservationService = ActivatorUtilities.CreateInstance<ActivityReservationService>(
            services,
            queueService);

        IQueuedWorkWithdrawalService withdrawalService = ActivatorUtilities.CreateInstance<QueuedWorkWithdrawalService>(
            services,
            queueService,
            reservationService);

        var routingService = new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(services.GetRequiredService<IClock>()),
            new LongestIdleRoutingStrategy(),
        ], services.GetRequiredService<IClock>());

        IActivityAssignmentService assignmentService = ActivatorUtilities.CreateInstance<ActivityAssignmentService>(
            services,
            reservationService,
            withdrawalService,
            (IActivityRoutingService)routingService,
            businessHours.Object,
            new QueueAvailability(harness));

        return new QueuedWorkWithdrawalFixture(harness, reservationService, withdrawalService, assignmentService);
    }

    /// <summary>
    /// Seeds a waiting item in the campaign queue for an activity in <paramref name="status"/>.
    /// </summary>
    public async Task<(OmnichannelActivity Activity, QueueItem QueueItem)> SeedAsync(
        string activityId,
        ActivityStatus status,
        bool storeActivity = true)
    {
        var activity = new OmnichannelActivity
        {
            ItemId = activityId,
            CampaignId = DialerModeIntegrationHarness.CampaignId,
            InteractionType = ActivityInteractionType.Manual,
            Status = status,
            PreferredDestination = "+15550000001",
        };

        var queueItem = await Harness.SeedQueuedActivityAsync(activity, CampaignQueueId, storeActivity);

        // Each seeded item waits a moment longer than the last, so the first seeded is the queue's head.
        Harness.Clock.Advance(TimeSpan.FromSeconds(1));

        return (activity, queueItem);
    }

    public async Task<QueueItem> FindQueueItemAsync(string queueItemId)
        => await QueueItems.FindByIdAsync(queueItemId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Creates the CRM's activity manager with the Contact Center's routability handler attached, acting as
    /// <paramref name="currentUserId"/>, so a test can close an activity the way the admin screens do. The handler's
    /// deferred withdrawal runs when the harness commits.
    /// </summary>
    public IOmnichannelActivityManager CreateActivityManager(string currentUserId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, currentUserId)], "Test")),
        };

        var handler = new ContactCenterActivityRoutabilityHandler(
            new WithdrawalScopeExecutor((HarnessScopeExecutor)Harness.Services.GetRequiredService<IContactCenterScopeExecutor>(), WithdrawalService),
            new HttpContextAccessor { HttpContext = httpContext });

        return new OmnichannelActivityManager(
            Mock.Of<IOmnichannelActivityStore>(),
            [handler],
            NullLogger<CatalogManager<OmnichannelActivity>>.Instance);
    }

    public ValueTask DisposeAsync() => Harness.DisposeAsync();

    /// <summary>
    /// Defers onto the harness's after-commit queue, handing withdrawal work the fixture's real withdrawal service.
    /// </summary>
    private sealed class WithdrawalScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly HarnessScopeExecutor _inner;
        private readonly IQueuedWorkWithdrawalService _withdrawal;

        public WithdrawalScopeExecutor(HarnessScopeExecutor inner, IQueuedWorkWithdrawalService withdrawal)
        {
            _inner = inner;
            _withdrawal = withdrawal;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => operation is Func<IQueuedWorkWithdrawalService, Task> withdraw ? withdraw(_withdrawal) : _inner.ExecuteAsync(operation);

        public Task ExecuteAsync(Func<IServiceProvider, Task> operation) => _inner.ExecuteAsync(operation);

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => operation is Func<IQueuedWorkWithdrawalService, Task> withdraw
                ? _inner.ScheduleAfterCommit(() => withdraw(_withdrawal))
                : _inner.ScheduleAfterCommit(operation);

        public bool ScheduleAfterCommit(Func<Task> operation) => _inner.ScheduleAfterCommit(operation);
    }

    /// <summary>
    /// Reports every signed-in agent who is Available with no offer ringing as available for any queue.
    /// </summary>
    private sealed class QueueAvailability : IAgentAvailabilityService
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly HarnessAvailabilityService _inner;

        public QueueAvailability(DialerModeIntegrationHarness harness)
        {
            _harness = harness;
            _inner = new HarnessAvailabilityService(harness.AgentManager);
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
                var availability = await _inner.GetAsync(agentId, queueId, cancellationToken);

                if (availability is not null)
                {
                    available.Add(availability);
                }
            }

            return available;
        }
    }
}
