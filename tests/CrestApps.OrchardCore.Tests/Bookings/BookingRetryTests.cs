using System.Reflection;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// A payment notice, a cancel or a "mark as done" that arrives twice changes a booking once: its seats are taken or given
/// back once, and its buyer and its order hear about it once. Workflow events follow the booking's status changing, so a
/// second call that changes nothing raises none.
/// </summary>
public sealed class BookingRetryTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<BookingSlot> _slots = [];
    private readonly Order _order = new() { ItemId = "order-1", OrderNumber = "1001", CustomerEmail = "ann@contoso.com", CustomerName = "Ann" };
    private readonly Booking _booking = new()
    {
        ItemId = "booking-1",
        OrderId = "order-1",
        ContentItemId = "tour",
        Title = "Coastal tour",
        SlotKeys = ["2026-12-09"],
        Seats = 2,
        Status = BookingStatus.Held,
        Total = 400m,
        Currency = "USD",
        StartsUtc = new DateTime(2026, 12, 9, 12, 0, 0, DateTimeKind.Utc),
    };

    private readonly Mock<IBookingStore> _bookings = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<ICommerceEmailSender> _emailSender = new();

    public BookingRetryTests()
    {
        _slots.Add(new BookingSlot { ItemId = "slot-1", ContentItemId = "tour", SlotKey = "2026-12-09", Capacity = 10, Held = 2 });

        _bookings.Setup(store => store.ListByOrderAsync(_order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(() => [_booking]);
        _bookings.Setup(store => store.FindByIdAsync(_booking.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(() => _booking);
        _emailSender.SetupGet(sender => sender.CanSend).Returns(true);
        _emailSender.Setup(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task APaymentNoticeSentTwice_TakesTheSeatsOnce_AndConfirmsOnce()
    {
        var service = CreateService();

        await service.ConfirmPaidAsync(_order, TestContext.Current.CancellationToken);
        await service.ConfirmPaidAsync(_order, TestContext.Current.CancellationToken);

        Assert.Equal(BookingStatus.Confirmed, _booking.Status);
        Assert.Equal((0, 2), (_slots[0].Held, _slots[0].Sold));
        _bookings.Verify(store => store.UpdateAsync(_booking, It.IsAny<CancellationToken>()), Times.Once());
        _emailSender.Verify(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ACancelSentTwice_GivesTheSeatsBackOnce()
    {
        var service = CreateService();

        _booking.Status = BookingStatus.Confirmed;
        _slots[0].Held = 0;
        _slots[0].Sold = 2;

        var first = await service.CancelAsync(_booking, refund: null, reason: null, actorName: "admin", TestContext.Current.CancellationToken);
        var second = await service.CancelAsync(_booking, refund: null, reason: null, actorName: "admin", TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(BookingStatus.Canceled, _booking.Status);
        Assert.Equal((0, 0), (_slots[0].Held, _slots[0].Sold));
        _bookings.Verify(store => store.UpdateAsync(_booking, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task MarkingDoneTwice_ChangesTheBookingOnce()
    {
        var orders = new Mock<IOrderStore>();

        orders.Setup(store => store.FindByIdAsync(_order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_order);

        var service = CreateService(orders.Object);

        _booking.Status = BookingStatus.Confirmed;

        var first = await service.FinishAsync(_booking, noShow: false, "admin", TestContext.Current.CancellationToken);
        var second = await service.FinishAsync(_booking, noShow: true, "admin", TestContext.Current.CancellationToken);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(BookingStatus.Completed, _booking.Status);
        _bookings.Verify(store => store.UpdateAsync(_booking, It.IsAny<CancellationToken>()), Times.Once());
        _orderService.Verify(service => service.AddNoteAsync(_order, It.IsAny<string>(), It.IsAny<OrderNoteKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    private BookingService CreateService(IOrderStore orders = null)
    {
        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();

        clock.Setup(c => c.UtcNow).Returns(_now);

        var balances = new Mock<IBookingBalanceStore>();

        balances.Setup(store => store.FindByOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((BookingBalance)null);

        var emails = new BookingEmailSender(_emailSender.Object, siteService.Object, HtmlEncoder.Default, new PassThroughStringLocalizer<BookingEmailSender>());

        if (orders is null)
        {
            var noOrders = new Mock<IOrderStore>();

            noOrders.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Order)null);
            orders = noOrders.Object;
        }

        // Built from the services it's given, so a dependency added later that these paths don't use is passed as null.
        return Create<BookingService>(
            _bookings.Object,
            balances.Object,
            CreateCapacity(siteService.Object, clock.Object),
            orders,
            _orderService.Object,
            emails,
            clock.Object,
            NullLogger<BookingService>.Instance,
            new PassThroughStringLocalizer<BookingService>(),
            Array.Empty<IBookingPaymentHandler>());
    }

    private BookingCapacityService CreateCapacity(ISiteService siteService, IClock clock)
    {
        var slots = new Mock<IBookingSlotStore>();

        slots.Setup(store => store.ListAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string contentItemId, IEnumerable<string> keys, CancellationToken _) => [.. _slots.Where(slot => slot.ContentItemId == contentItemId && keys.Contains(slot.SlotKey))]);
        slots.Setup(store => store.UpdateAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var locks = new Mock<IDistributedLock>();

        locks.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        return new BookingCapacityService(slots.Object, Mock.Of<IBookingStore>(), Mock.Of<ISession>(), locks.Object, siteService, clock, NullLogger<BookingCapacityService>.Instance);
    }

    private static T Create<T>(params object[] services)
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => services.FirstOrDefault(service => service is not null && parameter.ParameterType.IsInstanceOfType(service)))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
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
