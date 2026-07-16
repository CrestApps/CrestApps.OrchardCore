using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class ActivityQueueServiceConcurrencyTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EnqueueAsync_WhenConcurrentClaimWins_ReturnsPersistedQueueItem()
    {
        // Arrange
        var existing = new QueueItem
        {
            ItemId = "queue-item-1",
            ActivityItemId = "activity-1",
            QueueId = "queue-1",
            Status = QueueItemStatus.Waiting,
            EnqueuedUtc = _now,
        };
        var scopedService = Mock.Of<IActivityQueueService>(service =>
            service.EnqueueAsync("activity-1", "queue-1", null, It.IsAny<CancellationToken>()) == Task.FromResult(existing));
        var scopeExecutor = new SingleContextScopeExecutor<IActivityQueueService>(scopedService);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(service => service.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem)null);
        queueItemManager
            .Setup(service => service.NewAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem());
        queueItemManager
            .Setup(service => service.CreateAsync(It.IsAny<QueueItem>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TestDbException());

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(service => service.FindByIdAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue
            {
                ItemId = "queue-1",
                DefaultPriority = InteractionPriority.High,
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        var service = new ActivityQueueService(
            queueItemManager.Object,
            queueManager.Object,
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IBusinessHoursService>(),
            Mock.Of<IContactCenterEventPublisher>(),
            Mock.Of<ISession>(),
            scopeExecutor,
            clock.Object);

        // Act
        var result = await service.EnqueueAsync("activity-1", "queue-1", null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(existing, result);
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
