using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A waiting caller's maximum wait and overflow hop are applied when they fall due, not at the next queue-treatment
/// sweep. That sweep shares Orchard's one-at-a-time background loop with tasks that hold it for most of a minute, so
/// a deadline between its runs waited for the next one. Runs the real queue and limit services over the harness's
/// SQLite store with time driven by the test and no sweep ever run.
/// </summary>
public sealed class QueueWaitDeadlineIntegrationTests
{
    private const string SupportQueueId = "queue-1";
    private const string OverflowQueueId = "queue-2";

    [Fact]
    public async Task AWaitingCaller_GoesToVoicemailAtTheirMaximumWait_WithoutTheSweep()
    {
        // Arrange
        await using var context = await QueueWaitContext.CreateAsync(new ActivityQueue
        {
            ItemId = SupportQueueId,
            Enabled = true,
            MaxWaitSeconds = 60,
            MaxWaitAction = QueueMaxWaitAction.Voicemail,
        });
        var item = await context.EnqueueAsync();

        // Act
        await context.AdvanceAsync(TimeSpan.FromSeconds(59.5));
        var sentBeforeDue = context.Voicemailed.Count;

        await context.AdvanceAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(0, sentBeforeDue);
        Assert.Equal(item.ItemId, Assert.Single(context.Voicemailed));
    }

    [Fact]
    public async Task AWaitingCaller_OverflowsAtTheirHop_WithoutTheSweep()
    {
        // Arrange
        await using var context = await QueueWaitContext.CreateAsync(new ActivityQueue
        {
            ItemId = SupportQueueId,
            Enabled = true,
            OverflowTargets = [new QueueOverflowTarget { QueueId = OverflowQueueId, AfterSeconds = 20 }],
        });
        var item = await context.EnqueueAsync();

        // Act
        await context.AdvanceAsync(TimeSpan.FromSeconds(19.5));
        var queueBeforeDue = (await context.FindItemAsync(item.ItemId)).QueueId;

        await context.AdvanceAsync(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(SupportQueueId, queueBeforeDue);
        Assert.Equal(OverflowQueueId, (await context.FindItemAsync(item.ItemId)).QueueId);
        Assert.Single(context.Harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.QueueItemOverflowed && e.AggregateId == item.ItemId);
    }

    [Fact]
    public async Task ACallerRingingAnAgentAtTheirMaximumWait_IsLeftAlone_ThenSentOnWhenTheOfferEnds()
    {
        // Arrange
        await using var context = await QueueWaitContext.CreateAsync(new ActivityQueue
        {
            ItemId = SupportQueueId,
            Enabled = true,
            MaxWaitSeconds = 60,
            MaxWaitAction = QueueMaxWaitAction.Voicemail,
        });
        var item = await context.EnqueueAsync();
        var stored = await context.FindItemAsync(item.ItemId);
        stored.TransitionTo(QueueItemStatus.Reserved);
        await context.Harness.Services.GetRequiredService<IQueueItemManager>().UpdateAsync(stored, cancellationToken: TestContext.Current.CancellationToken);
        await context.Harness.CommitAsync();

        // Act: the maximum wait passes while an offer rings, then the offer ends and the caller is back.
        await context.AdvanceAsync(TimeSpan.FromSeconds(70));
        var sentWhileRinging = context.Voicemailed.Count;

        stored = await context.FindItemAsync(item.ItemId);
        stored.TransitionTo(QueueItemStatus.Waiting);
        await context.Harness.Services.GetRequiredService<IQueueItemManager>().UpdateAsync(stored, cancellationToken: TestContext.Current.CancellationToken);
        await context.Harness.CommitAsync();
        await context.Enforcer.ArmAsync(item.ItemId, TestContext.Current.CancellationToken);
        await context.AdvanceAsync(TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Equal(0, sentWhileRinging);
        Assert.Equal(item.ItemId, Assert.Single(context.Voicemailed));
    }

    [Fact]
    public async Task ACallerWhoLeavesTheQueue_HasTheirDeadlineDropped()
    {
        // Arrange
        await using var context = await QueueWaitContext.CreateAsync(new ActivityQueue
        {
            ItemId = SupportQueueId,
            Enabled = true,
            MaxWaitSeconds = 60,
            MaxWaitAction = QueueMaxWaitAction.Voicemail,
        });
        var item = await context.EnqueueAsync();

        // Act
        await context.Handler.HandleAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemDequeued,
            AggregateType = nameof(QueueItem),
            AggregateId = item.ItemId,
        }, TestContext.Current.CancellationToken);
        await context.AdvanceAsync(TimeSpan.FromSeconds(120));

        // Assert
        Assert.Empty(context.Voicemailed);
        Assert.Equal(0, context.Scheduler.Count);
    }

    /// <summary>
    /// The real queue and limit services over the harness, a recording voicemail sink, and a deadline scheduler on
    /// test-driven time.
    /// </summary>
    private sealed class QueueWaitContext : IAsyncDisposable
    {
        private QueueWaitContext(
            DialerModeIntegrationHarness harness,
            ManualTimeProvider time,
            ContactCenterDeadlineScheduler scheduler,
            QueueWaitDeadlineEnforcer enforcer,
            List<string> voicemailed)
        {
            Harness = harness;
            Time = time;
            Scheduler = scheduler;
            Enforcer = enforcer;
            Voicemailed = voicemailed;
            Handler = new QueueWaitDeadlineEventHandler(
                new Lazy<IQueueWaitDeadlineEnforcer>(() => enforcer),
                scheduler,
                harness.Services.GetRequiredService<IActivityReservationManager>());
        }

        public DialerModeIntegrationHarness Harness { get; }

        public ManualTimeProvider Time { get; }

        public ContactCenterDeadlineScheduler Scheduler { get; }

        public QueueWaitDeadlineEnforcer Enforcer { get; }

        public QueueWaitDeadlineEventHandler Handler { get; }

        public List<string> Voicemailed { get; }

        public static async Task<QueueWaitContext> CreateAsync(ActivityQueue queue)
        {
            var harness = await DialerModeIntegrationHarness.CreateAsync();
            var services = harness.Services;
            var time = new ManualTimeProvider(harness.Clock);

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id == queue.ItemId
                    ? queue
                    : new ActivityQueue { ItemId = id, Enabled = true });

            var businessHours = new Mock<IBusinessHoursService>();
            businessHours
                .Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            IActivityQueueService queueService = ActivatorUtilities.CreateInstance<ActivityQueueService>(
                services,
                queueManager.Object,
                businessHours.Object,
                Mock.Of<IQueueTreatmentProvider>());

            var voicemailed = new List<string>();
            var sink = new Mock<IWaitingCallVoicemailSink>();
            sink
                .Setup(s => s.SendToVoicemailAsync(It.IsAny<QueueItem>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((QueueItem item, string _, CancellationToken _) => voicemailed.Add(item.ItemId))
                .ReturnsAsync(true);

            var limitService = new QueueLimitService(
                services.GetRequiredService<IQueueItemManager>(),
                queueManager.Object,
                queueService,
                sink.Object,
                harness.Clock,
                NullLogger<QueueLimitService>.Instance);

            QueueWaitDeadlineEnforcer enforcer = null;
            var scopeServices = new ScopeServices(harness, () => enforcer);
            var scheduler = new ContactCenterDeadlineScheduler(
                harness.Clock,
                time,
                async work =>
                {
                    await work(scopeServices);
                    await harness.CommitAsync();
                },
                NullLogger.Instance);

            enforcer = new QueueWaitDeadlineEnforcer(
                scheduler,
                services.GetRequiredService<IQueueItemManager>(),
                queueManager.Object,
                harness.InteractionManager,
                queueService,
                limitService,
                [],
                harness.Clock);

            return new QueueWaitContext(harness, time, scheduler, enforcer, voicemailed);
        }

        /// <summary>
        /// Seats one caller in the support queue, then delivers the enqueue event as the outbox would.
        /// </summary>
        public async Task<QueueItem> EnqueueAsync()
        {
            var item = await Harness.SeedQueuedActivityAsync(
                new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Pending },
                SupportQueueId);

            await Handler.HandleAsync(new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.QueueItemAdded,
                AggregateType = nameof(QueueItem),
                AggregateId = item.ItemId,
            }, TestContext.Current.CancellationToken);

            return item;
        }

        public async Task<QueueItem> FindItemAsync(string queueItemId)
            => await Harness.Services.GetRequiredService<IQueueItemManager>().FindByIdAsync(queueItemId, TestContext.Current.CancellationToken);

        public Task AdvanceAsync(TimeSpan by) => Time.AdvanceAsync(by, Scheduler.WhenIdleAsync);

        public async ValueTask DisposeAsync()
        {
            Scheduler.Dispose();
            await Harness.DisposeAsync();
        }
    }

    /// <summary>
    /// What a deadline's shell scope resolves: the context's enforcer, and the harness for everything else.
    /// </summary>
    private sealed class ScopeServices : IServiceProvider
    {
        private readonly DialerModeIntegrationHarness _harness;
        private readonly Func<IQueueWaitDeadlineEnforcer> _enforcer;

        public ScopeServices(DialerModeIntegrationHarness harness, Func<IQueueWaitDeadlineEnforcer> enforcer)
        {
            _harness = harness;
            _enforcer = enforcer;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(IQueueWaitDeadlineEnforcer)
                ? _enforcer()
                : _harness.Services.GetService(serviceType);
    }
}
