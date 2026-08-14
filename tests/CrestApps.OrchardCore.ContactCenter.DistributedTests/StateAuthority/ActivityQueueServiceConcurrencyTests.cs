using System.Data.Common;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class ActivityQueueServiceConcurrencyTests
{
    private const string ActivityItemId = "activity-1";
    private const string QueueId = "queue-1";
    private const int RacingWorkers = 8;

    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EnqueueAsync_WhenManyWorkersRaceForTheSameActivity_PersistsExactlyOneActiveQueueItem()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "activity-queue-race",
            [new QueueItemIndexProvider()],
            ContactCenterStoreTestHarness.CreateQueueItemSchemaAsync);

        var sessions = new List<ISession>();

        try
        {
            var workers = Enumerable
                .Range(0, RacingWorkers)
                .Select(_ => CreateWorker(harness, sessions))
                .ToArray();

            // Act
            var outcomes = await Task.WhenAll(workers.Select(EnqueueAsync));

            // Assert
            var winners = outcomes.Where(outcome => outcome.Item is not null).ToArray();
            Assert.NotEmpty(winners);

            Assert.All(
                winners,
                outcome => Assert.Equal(QueueItemStatus.Waiting, outcome.Item.Status));

            var persisted = await ReadQueueItemsAsync(harness);
            var claimed = Assert.Single(persisted);
            Assert.Equal(ActivityItemId, claimed.ActivityItemId);

            Assert.All(
                winners,
                outcome => Assert.Equal(claimed.ItemId, outcome.Item.ItemId));

            Assert.All(
                outcomes.Where(outcome => outcome.Item is null),
                outcome => Assert.True(
                    outcome.Exception is ConcurrencyException or DbException,
                    $"Expected a claim conflict but received {outcome.Exception?.GetType().Name ?? "no exception"}."));
        }
        finally
        {
            foreach (var session in sessions)
            {
                await session.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task EnqueueAsync_WhenTheClaimIsLostToAConcurrentWriter_ReturnsTheWinnerFromAFreshScope()
    {
        // Arrange
        var existing = new QueueItem
        {
            ItemId = "queue-item-1",
            ActivityItemId = ActivityItemId,
            QueueId = QueueId,
            EnqueuedUtc = _now,
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        var scopedService = Mock.Of<IActivityQueueService>(service =>
            service.EnqueueAsync(ActivityItemId, QueueId, null, It.IsAny<CancellationToken>()) == Task.FromResult(existing));
        var scopeExecutor = new SingleContextScopeExecutor<IActivityQueueService>(scopedService);
        var service = CreateConflictingService(scopeExecutor, out var scopeExecutions);

        // Act
        var result = await service.EnqueueAsync(ActivityItemId, QueueId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(existing, result);
        Assert.Equal(1, scopeExecutions());
    }

    [Fact]
    public async Task EnqueueAsync_WhenEveryFreshScopeAlsoConflicts_ExhaustsTheRetriesAndThrows()
    {
        // Arrange
        var scopedService = new Mock<IActivityQueueService>();
        scopedService
            .Setup(service => service.EnqueueAsync(ActivityItemId, QueueId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TestDbException());

        var scopeExecutor = new SingleContextScopeExecutor<IActivityQueueService>(scopedService.Object);
        var service = CreateConflictingService(scopeExecutor, out var scopeExecutions);

        // Act
        await Assert.ThrowsAsync<TestDbException>(
            () => service.EnqueueAsync(ActivityItemId, QueueId, null, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(scopeExecutions() > 1, "The service must retry the claim in more than one fresh scope.");
    }

    private static ActivityQueueService CreateConflictingService(
        IContactCenterScopeExecutor scopeExecutor,
        out Func<int> scopeExecutions)
    {
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(service => service.FindByActivityIdAsync(ActivityItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem)null);
        queueItemManager
            .Setup(service => service.NewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem());
        queueItemManager
            .Setup(service => service.CreateAsync(It.IsAny<QueueItem>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TestDbException());

        var countingExecutor = new CountingScopeExecutor(scopeExecutor);
        scopeExecutions = () => countingExecutor.ExecutionCount;

        return new ActivityQueueService(
            queueItemManager.Object,
            CreateQueueManager(),
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IContactCenterWorkStateService>(),
            Mock.Of<IBusinessHoursService>(),
            Mock.Of<IContactCenterEventPublisher>(),
            Mock.Of<ISession>(),
            countingExecutor,
            CreateClock());
    }

    private static ActivityQueueService CreateWorker(
        ContactCenterStoreTestHarness harness,
        List<ISession> sessions)
    {
        var session = harness.Store.CreateSession();

        lock (sessions)
        {
            sessions.Add(session);
        }

        return new ActivityQueueService(
            new QueueItemManager(
                new QueueItemStore(session),
                [],
                NullLogger<CatalogManager<QueueItem>>.Instance),
            CreateQueueManager(),
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IContactCenterWorkStateService>(),
            Mock.Of<IBusinessHoursService>(),
            Mock.Of<IContactCenterEventPublisher>(),
            session,
            new NoRetryScopeExecutor(),
            CreateClock());
    }

    private static async Task<Outcome> EnqueueAsync(ActivityQueueService service)
    {
        await Task.Yield();

        try
        {
            return new Outcome(
                await service.EnqueueAsync(ActivityItemId, QueueId, null, TestContext.Current.CancellationToken),
                null);
        }
        catch (Exception exception)
        {
            return new Outcome(null, exception);
        }
    }

    private static async Task<IReadOnlyCollection<QueueItem>> ReadQueueItemsAsync(ContactCenterStoreTestHarness harness)
    {
        await using var session = harness.Store.CreateSession();

        return await new QueueItemStore(session).GetWaitingAsync(QueueId, TestContext.Current.CancellationToken);
    }

    private static IActivityQueueManager CreateQueueManager()
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(service => service.FindByIdAsync(QueueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue
            {
                ItemId = QueueId,
                DefaultPriority = InteractionPriority.High,
            });

        return queueManager.Object;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed record Outcome(QueueItem Item, Exception Exception);

    private sealed class NoRetryScopeExecutor : IContactCenterScopeExecutor
    {
        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            throw new TestDbException();
        }

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            return false;
        }

        public bool ScheduleAfterCommit(Func<Task> operation)
        {
            return false;
        }
    }

    private sealed class CountingScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly IContactCenterScopeExecutor _inner;
        private int _executionCount;

        public CountingScopeExecutor(IContactCenterScopeExecutor inner)
        {
            _inner = inner;
        }

        public int ExecutionCount => _executionCount;

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            Interlocked.Increment(ref _executionCount);

            return _inner.ExecuteAsync(operation);
        }

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            return _inner.ScheduleAfterCommit(operation);
        }

        public bool ScheduleAfterCommit(Func<Task> operation)
        {
            return _inner.ScheduleAfterCommit(operation);
        }
    }

    private sealed class SingleContextScopeExecutor<TContext> : IContactCenterScopeExecutor
        where TContext : notnull
    {
        private readonly TContext _context;

        public SingleContextScopeExecutor(TContext context)
        {
            _context = context;
        }

        public Task ExecuteAsync<TRequestedContext>(Func<TRequestedContext, Task> operation)
            where TRequestedContext : notnull
        {
            return operation((TRequestedContext)(object)_context);
        }

        public bool ScheduleAfterCommit<TRequestedContext>(Func<TRequestedContext, Task> operation)
            where TRequestedContext : notnull
        {
            return false;
        }

        public bool ScheduleAfterCommit(Func<Task> operation)
        {
            return false;
        }
    }

    private sealed class TestDbException : DbException
    {
    }
}
