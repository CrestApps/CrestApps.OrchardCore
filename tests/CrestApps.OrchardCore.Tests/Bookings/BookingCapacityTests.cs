using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// A booking holds its dates all or none, can't be bought beyond what's left on them, and is priced for its dates and
/// the people in it.
/// </summary>
public sealed class BookingCapacityTests
{
    private readonly List<BookingSlot> _slots = [];
    private readonly List<Booking> _bookings = [];

    [Fact]
    public async Task ADeparture_HoldsOnlyTheDatesTheStoreAdded_AndOnlyWhatsLeft()
    {
        var capacity = CreateCapacity();
        var tour = new BookingPart { Kind = BookingKind.Departure, Capacity = 12 };

        _slots.Add(new BookingSlot { ItemId = "s1", ContentItemId = "tour", SlotKey = "2026-12-09", Capacity = 2 });

        // A date the store didn't add isn't a departure.
        Assert.False(await capacity.HoldAsync(Booking("tour", ["2026-12-10"], seats: 1), tour, TestContext.Current.CancellationToken));

        // A double room takes the last two seats; a third person finds none.
        Assert.True(await capacity.HoldAsync(Booking("tour", ["2026-12-09"], seats: 2), tour, TestContext.Current.CancellationToken));
        Assert.Equal(2, _slots[0].Held);
        Assert.False(await capacity.HoldAsync(Booking("tour", ["2026-12-09"], seats: 1), tour, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AStay_IsMadeOfItsNights_AndAFullNightRefusesTheWholeStay()
    {
        var capacity = CreateCapacity();
        var room = new BookingPart { Kind = BookingKind.Nightly, Capacity = 1 };

        _slots.Add(new BookingSlot { ItemId = "n2", ContentItemId = "room", SlotKey = "2026-12-05", Capacity = 1, Sold = 1 });

        Assert.False(await capacity.HoldAsync(Booking("room", ["2026-12-04", "2026-12-05"], seats: 1), room, TestContext.Current.CancellationToken));

        // Nothing was held for the first night either.
        Assert.DoesNotContain(_slots, slot => slot.SlotKey == "2026-12-04");

        Assert.True(await capacity.HoldAsync(Booking("room", ["2026-12-06", "2026-12-07"], seats: 1), room, TestContext.Current.CancellationToken));
        Assert.Equal(2, _slots.Count(slot => slot.Held == 1));
    }

    [Fact]
    public async Task Paying_TakesTheHeldSeats_AndCanceling_GivesThemBack()
    {
        var capacity = CreateCapacity();
        var tour = new BookingPart { Kind = BookingKind.Departure };
        var booking = Booking("tour", ["2026-12-09"], seats: 2);

        _slots.Add(new BookingSlot { ItemId = "s1", ContentItemId = "tour", SlotKey = "2026-12-09", Capacity = 4 });

        await capacity.HoldAsync(booking, tour, TestContext.Current.CancellationToken);
        await capacity.CommitAsync(booking, TestContext.Current.CancellationToken);

        Assert.Equal((0, 2), (_slots[0].Held, _slots[0].Sold));

        await capacity.ReleaseAsync(booking, sold: true, TestContext.Current.CancellationToken);

        Assert.Equal((0, 0), (_slots[0].Held, _slots[0].Sold));
    }

    [Fact]
    public async Task ALine_IsPricedForItsPeople_AndCantBeBoughtBeyondWhatsLeft()
    {
        var handler = new BookingCartLineHandler(CreateCapacity(), new PassThroughStringLocalizer<BookingCartLineHandler>());
        var item = new ContentItem { ContentItemId = "tour", DisplayText = "Tour" };

        item.Weld(new BookingPart
        {
            Kind = BookingKind.Departure,
            PricePerPerson = true,
            Occupancies =
            [
                new BookingOccupancy { Id = "double", Label = "Double room", People = 2 },
                new BookingOccupancy { Id = "single", Label = "Single room", People = 1, PriceAdjustment = 300m },
            ],
        });

        _slots.Add(new BookingSlot { ItemId = "s1", ContentItemId = "tour", SlotKey = "2026-12-09", Capacity = 3, Price = 1100m });

        var context = Evaluate(item, "double", quantity: 1);

        await handler.EvaluateAsync(context, TestContext.Current.CancellationToken);

        // The date's own price, for each of the two people; three seats leave room for one double.
        Assert.Equal(2200m, context.UnitPrice);
        Assert.Equal(2, context.PeoplePerUnit);
        Assert.Equal(1, context.MaxQuantity);
        Assert.Null(context.Problem);
        Assert.Contains(context.Details, detail => detail.Key == "booking.occupancy" && detail.Value == "Double room (2 people)");

        _slots[0].Sold = 2;

        var full = Evaluate(item, "double", quantity: 1);

        await handler.EvaluateAsync(full, TestContext.Current.CancellationToken);

        Assert.NotNull(full.Problem);
        Assert.Equal(0, full.MaxQuantity);
    }

    private static CartLineEvaluateContext Evaluate(ContentItem item, string occupancy, int quantity)
        => new()
        {
            ContentItem = item,
            Title = item.DisplayText,
            Quantity = quantity,
            UnitPrice = 1000m,
            MaxQuantity = 99,
            Line = new CartLine
            {
                ContentItemId = item.ContentItemId,
                Quantity = quantity,
                Details = new Dictionary<string, string> { ["booking.date"] = "2026-12-09", ["booking.occupancy"] = occupancy },
            },
        };

    private static Booking Booking(string contentItemId, List<string> keys, int seats)
        => new() { ItemId = Guid.NewGuid().ToString("N"), ContentItemId = contentItemId, SlotKeys = keys, Seats = seats };

    private BookingCapacityService CreateCapacity()
    {
        var slots = new Mock<IBookingSlotStore>();

        slots.Setup(store => store.ListAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string contentItemId, IEnumerable<string> keys, CancellationToken _) => [.. _slots.Where(slot => slot.ContentItemId == contentItemId && keys.Contains(slot.SlotKey))]);
        slots.Setup(store => store.CreateAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()))
            .Callback((BookingSlot slot, CancellationToken _) => _slots.Add(slot))
            .Returns(ValueTask.CompletedTask);
        slots.Setup(store => store.UpdateAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var bookings = new Mock<IBookingStore>();

        bookings.Setup(store => store.ListHeldByCartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cartId, string contentItemId, CancellationToken _) => [.. _bookings.Where(booking => booking.CartId == cartId && booking.ContentItemId == contentItemId)]);

        var locks = new Mock<IDistributedLock>();

        locks.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();

        clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));

        return new BookingCapacityService(slots.Object, bookings.Object, Mock.Of<ISession>(), locks.Object, siteService.Object, clock.Object, NullLogger<BookingCapacityService>.Instance);
    }

    private sealed class NoopLocker : ILocker
    {
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
