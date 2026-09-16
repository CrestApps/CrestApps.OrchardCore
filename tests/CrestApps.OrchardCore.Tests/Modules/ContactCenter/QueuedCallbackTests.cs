using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A callback that costs the caller their place in line is worse than waiting: they hang up expecting to be
/// treated as though they had held, and instead go to the back. The whole promise of "press 1 and we will call
/// you" is that the queue remembers when they arrived.
/// </summary>
public sealed class QueuedCallbackTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Accepting_SchedulesACallbackCarryingTheOriginalArrivalTime()
    {
        // Arrange
        var harness = new Harness();
        var item = Item(enqueuedUtc: _now.AddMinutes(-8));

        // Act
        var accepted = await harness.Service.AcceptAsync(item, "+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        Assert.NotNull(harness.Scheduled);
        Assert.Equal("+16502530000", harness.Scheduled.Destination);
        Assert.Equal("q1", harness.Scheduled.QueueId);

        // The place in line is the arrival time, not the moment they pressed the key. Stamping "now" would send
        // a caller who had already waited eight minutes to the back of the queue.
        Assert.Equal(_now.AddMinutes(-8), harness.Scheduled.RequestedUtc);
    }

    [Fact]
    public async Task Accepting_RemovesTheCallerFromTheQueue()
    {
        // Arrange
        // Leaving the item waiting would have an agent offered a caller who has already hung up.
        var harness = new Harness();
        var item = Item(enqueuedUtc: _now.AddMinutes(-8));

        // Act
        await harness.Service.AcceptAsync(item, "+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueItemStatus.Removed, item.Status);
    }

    [Fact]
    public async Task Accepting_WithNoNumberToCall_IsRefused()
    {
        // Arrange
        // Scheduling a callback with nowhere to call produces a job that can only ever fail, and the caller has
        // already hung up believing they will be phoned.
        var harness = new Harness();
        var item = Item(enqueuedUtc: _now.AddMinutes(-8));

        // Act
        var accepted = await harness.Service.AcceptAsync(item, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(accepted);
        Assert.Null(harness.Scheduled);
        Assert.NotEqual(QueueItemStatus.Removed, item.Status);
    }

    [Fact]
    public async Task Accepting_Twice_SchedulesOneCallback()
    {
        // Arrange
        // A caller who presses the key twice, or a redelivered DTMF event, must not produce two calls back.
        var harness = new Harness();
        var item = Item(enqueuedUtc: _now.AddMinutes(-8));

        // Act
        await harness.Service.AcceptAsync(item, "+16502530000", TestContext.Current.CancellationToken);
        var second = await harness.Service.AcceptAsync(item, "+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(second);
        harness.CallbackService.Verify(
            service => service.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static QueueItem Item(DateTime enqueuedUtc)
        => new()
        {
            ItemId = "i1",
            QueueId = "q1",
            ActivityItemId = "act1",
            EnqueuedUtc = enqueuedUtc,
            QueueEnteredUtc = enqueuedUtc,
        };

    private sealed class Harness
    {
        public Mock<ICallbackService> CallbackService { get; } = new();

        public CallbackRequest Scheduled { get; private set; }

        public QueuedCallbackService Service { get; }

        public Harness()
        {
            CallbackService
                .Setup(service => service.ScheduleAsync(It.IsAny<CallbackRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallbackRequest request, CancellationToken _) =>
                {
                    Scheduled = request;

                    return request;
                });

            var queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Service = new QueuedCallbackService(
                CallbackService.Object,
                queueItemManager.Object,
                clock.Object,
                NullLogger<QueuedCallbackService>.Instance);
        }
    }
}
