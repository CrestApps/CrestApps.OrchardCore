using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueueItemManagerTests
{
    [Fact]
    public async Task FindNextWaitingAsync_WhenSlaAgingDisabled_UsesBoundedStoreQuery()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            ItemId = "queue-1",
            EnableSlaAging = false,
            SlaThresholdSeconds = 60,
        };
        var expected = new QueueItem { ItemId = "item-1", QueueId = "queue-1" };
        var store = new Mock<IQueueItemStore>();
        store
            .Setup(s => s.FindNextWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var manager = CreateManager(store);

        // Act
        var actual = await manager.FindNextWaitingAsync(queue, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, actual);
        store.Verify(s => s.FindNextWaitingAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.ListWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindNextWaitingAsync_WhenSlaAgingEnabled_ScoresWaitingBacklog()
    {
        // Arrange
        var utcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var queue = new ActivityQueue
        {
            ItemId = "queue-1",
            EnableSlaAging = true,
            SlaThresholdSeconds = 60,
        };

        var newerHigherPriority = new QueueItem
        {
            ItemId = "newer",
            QueueId = "queue-1",
            Priority = InteractionPriority.Highest,
            EnqueuedUtc = utcNow,
        };
        var agedLowPriority = new QueueItem
        {
            ItemId = "aged",
            QueueId = "queue-1",
            Priority = InteractionPriority.Lowest,
            EnqueuedUtc = utcNow.AddSeconds(-600),
        };

        var store = new Mock<IQueueItemStore>();
        store
            .Setup(s => s.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([newerHigherPriority, agedLowPriority]);

        var manager = CreateManager(store);

        // Act
        var actual = await manager.FindNextWaitingAsync(queue, utcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(agedLowPriority, actual);
        store.Verify(s => s.ListWaitingAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.FindNextWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static QueueItemManager CreateManager(Mock<IQueueItemStore> store)
    {
        return new QueueItemManager(
            store.Object,
            [],
            NullLogger<CatalogManager<QueueItem>>.Instance);
    }
}
