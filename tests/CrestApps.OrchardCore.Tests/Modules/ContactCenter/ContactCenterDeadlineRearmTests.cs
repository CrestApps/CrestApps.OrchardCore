using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The in-process deadlines are lost with the process. After a restart, an offer that was already ringing was only
/// expired by the once-a-minute sweep, and a caller's overflow hop or maximum wait only by the next treatment sweep,
/// so the tenant re-arms them from the durable state as soon as it has started.
/// </summary>
public sealed class ContactCenterDeadlineRearmTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RearmAsync_ArmsTheDeadlineOfEveryOfferStillRinging()
    {
        // Arrange
        var ringing = CreateReservation("reservation-1", _now.AddSeconds(20));
        var (services, scheduler, _, _) = CreateServices([[ringing]], waiting: new Dictionary<string, string[]>());

        // Act
        await ContactCenterDeadlineRearm.RearmAsync(services, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        scheduler.Verify(
            value => value.Schedule(
                OfferDeadlineEventHandler.GetDeadlineKey("reservation-1"),
                _now.AddSeconds(20),
                It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task RearmAsync_ReadsEveryPageOfRingingOffers()
    {
        // Arrange
        var first = CreateReservation("reservation-1", _now.AddSeconds(5));
        var second = CreateReservation("reservation-2", _now.AddSeconds(25));
        var (services, scheduler, _, _) = CreateServices([[first], [second]], waiting: new Dictionary<string, string[]>());

        // Act
        await ContactCenterDeadlineRearm.RearmAsync(services, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        scheduler.Verify(
            value => value.Schedule(OfferDeadlineEventHandler.GetDeadlineKey("reservation-2"), _now.AddSeconds(25), It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()),
            Times.Once);
    }

    [Fact]
    public async Task RearmAsync_ArmsEveryWaitingCallersWaitDeadline_AndEachQueuesTreatment()
    {
        // Arrange
        var (services, _, waitEnforcer, treatmentEnforcer) = CreateServices(
            [],
            waiting: new Dictionary<string, string[]>
            {
                ["queue-1"] = ["item-1", "item-2"],
                [ContactCenterConstants.DirectRouting.QueueId] = ["item-3"],
            });

        // Act
        await ContactCenterDeadlineRearm.RearmAsync(services, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        waitEnforcer.Verify(value => value.ArmAsync("item-1", It.IsAny<CancellationToken>()), Times.Once);
        waitEnforcer.Verify(value => value.ArmAsync("item-2", It.IsAny<CancellationToken>()), Times.Once);
        waitEnforcer.Verify(value => value.ArmAsync("item-3", It.IsAny<CancellationToken>()), Times.Once);
        treatmentEnforcer.Verify(value => value.ArmAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RearmAsync_StillArmsTheWaitingCallers_WhenTheOffersCannotBeRead()
    {
        // Arrange
        var (services, _, waitEnforcer, _) = CreateServices(null, waiting: new Dictionary<string, string[]> { ["queue-1"] = ["item-1"] });

        // Act
        await ContactCenterDeadlineRearm.RearmAsync(services, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        waitEnforcer.Verify(value => value.ArmAsync("item-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivatedAsync_DoesNoScopedWorkWhileTheTenantIsActivating()
    {
        // Arrange
        // Running scoped work inside activation waits on the tenant that is still starting, and it never finishes
        // starting; the re-arm has to be deferred until activation is over.
        var rearm = new ContactCenterDeadlineRearm(NullLogger<ContactCenterDeadlineRearm>.Instance);

        // Act
        var activation = rearm.ActivatedAsync();

        // Assert
        Assert.True(activation.IsCompletedSuccessfully);
        await activation;
    }

    private static ActivityReservation CreateReservation(string reservationId, DateTime expiresUtc)
        => new()
        {
            ItemId = reservationId,
            AgentId = "agent-1",
            ActivityItemId = "activity-" + reservationId,
            ExpiresUtc = expiresUtc,
        };

    private static (IServiceProvider Services, Mock<IContactCenterDeadlineScheduler> Scheduler, Mock<IQueueWaitDeadlineEnforcer> WaitEnforcer, Mock<IQueueTreatmentDeadlineEnforcer> TreatmentEnforcer) CreateServices(
        ActivityReservation[][] reservationPages,
        IReadOnlyDictionary<string, string[]> waiting)
    {
        var reservations = new Mock<IActivityReservationManager>();

        if (reservationPages is null)
        {
            reservations
                .Setup(manager => manager.GetExpiredAsync(It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("The reservations could not be read."));
        }
        else
        {
            var pageIndex = 0;

            reservations
                .Setup(manager => manager.GetExpiredAsync(It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var index = pageIndex++;

                    if (index >= reservationPages.Length)
                    {
                        return new ExpiredReservationPage([], null, 0);
                    }

                    var page = reservationPages[index];
                    var hasMore = index < reservationPages.Length - 1;

                    return new ExpiredReservationPage(page, hasMore ? page[^1].ExpiresUtc : null, hasMore ? index + 1 : 0);
                });
        }

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore
            .Setup(store => store.GetWaitingQueueIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(waiting.Keys.ToArray());

        var queueItems = new Mock<IQueueItemManager>();

        foreach (var (queueId, itemIds) in waiting)
        {
            queueItems
                .Setup(manager => manager.GetWaitingAsync(queueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(itemIds.Select(itemId => new QueueItem { ItemId = itemId, QueueId = queueId }).ToArray());
        }

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var scheduler = new Mock<IContactCenterDeadlineScheduler>();
        var waitEnforcer = new Mock<IQueueWaitDeadlineEnforcer>();
        var treatmentEnforcer = new Mock<IQueueTreatmentDeadlineEnforcer>();

        var services = new ServiceCollection()
            .AddSingleton(reservations.Object)
            .AddSingleton(queueItemStore.Object)
            .AddSingleton(queueItems.Object)
            .AddSingleton(clock.Object)
            .AddSingleton(scheduler.Object)
            .AddSingleton(waitEnforcer.Object)
            .AddSingleton(treatmentEnforcer.Object)
            .BuildServiceProvider();

        return (services, scheduler, waitEnforcer, treatmentEnforcer);
    }
}
