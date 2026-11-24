using System.Reflection;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.AI;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Agent.Indexing;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Bookings.AI;

/// <summary>
/// The bookings tools need Manage bookings, build the bookings list's query, finish only a confirmed booking, cancel with
/// exactly the refund given (in the booking's currency, never more than was paid, everything only with refundAll) under an
/// idempotency key, charge the balance only for the amount owed under a key, and keep the dates page's rules.
/// </summary>
public sealed class BookingAIToolTests
{
    private static readonly DateTime _now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly Order _order = new() { ItemId = "order-1", OrderNumber = "1042", Currency = "USD", Total = 400m, CustomerName = "Dana Reed", CustomerEmail = "dana@example.com" };
    private readonly Booking _booking = new()
    {
        ItemId = "booking-1",
        OrderId = "order-1",
        OrderNumber = "1042",
        ContentItemId = "tour",
        Title = "Coastal hike",
        SlotKeys = ["2026-12-09"],
        Seats = 2,
        People = 2,
        Status = BookingStatus.Confirmed,
        Total = 400m,
        Currency = "USD",
        CustomerName = "Dana Reed",
        CustomerEmail = "dana@example.com",
        StartsUtc = new DateTime(2026, 12, 9, 9, 0, 0, DateTimeKind.Utc),
        Travelers = [new System.Text.Json.Nodes.JsonObject { ["passport"] = "X1234567" }],
    };

    private readonly BookingBalance _balance = new()
    {
        ItemId = "balance-1",
        OrderId = "order-1",
        OrderNumber = "1042",
        TransactionId = "transaction-1",
        Amount = 300m,
        Currency = "USD",
        Status = BookingBalanceStatus.Scheduled,
    };

    private readonly List<BookingSlot> _slots = [new BookingSlot { ItemId = "slot-1", ContentItemId = "tour", SlotKey = "2026-12-09", Capacity = 10, Held = 3, Sold = 2 }];
    private readonly Mock<IBookingStore> _bookings = new();
    private readonly Mock<IBookingSlotStore> _slotStore = new();
    private readonly Mock<IBookingBalanceStore> _balances = new();
    private readonly Mock<IOrderStore> _orders = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly Mock<IOrderRefunder> _refunder = new();
    private readonly Mock<ITransactionManager> _transactions = new();
    private readonly Mock<IContentManager> _contentManager = new();
    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private BookingQuery _query;

    public BookingAIToolTests()
    {
        _bookings.Setup(store => store.FindByIdAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => _booking);
        _bookings.Setup(store => store.ListByOrderAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => [_booking]);
        _bookings.Setup(store => store.PageAsync(It.IsAny<BookingQuery>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((BookingQuery query, DateTime _, int _, int _, CancellationToken _) => _query = query)
            .ReturnsAsync((1, (IReadOnlyList<Booking>)[_booking]));
        _balances.Setup(store => store.FindByOrderAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => _balance);
        _orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
        _refunder.Setup(refunder => refunder.GetMoneyAsync(_order, It.IsAny<CancellationToken>())).ReturnsAsync((400m, 0m));
        _refunder.Setup(refunder => refunder.RefundAsync(_order, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order _, decimal amount, string _, string _, CancellationToken _) => (amount, (IReadOnlyList<string>)[]));
        _transactions.Setup(manager => manager.FindByIdAsync("transaction-1", It.IsAny<CancellationToken>())).ReturnsAsync((Transaction)null);
        _slotStore.Setup(store => store.ListAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string contentItemId, IEnumerable<string> keys, CancellationToken _) => [.. _slots.Where(slot => slot.ContentItemId == contentItemId && keys.Contains(slot.SlotKey))]);
        _slotStore.Setup(store => store.ListAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [.. _slots]);
        _slotStore.Setup(store => store.FindByIdAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => _slots[0]);

        var product = new ContentItem { ContentItemId = "tour", DisplayText = "Coastal hike" };
        product.Alter<BookingPart>(part => part.Capacity = 8);
        _contentManager.Setup(manager => manager.GetAsync("tour", It.IsAny<VersionOptions>())).ReturnsAsync(product);
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListBookingsTool.TheName,
        GetBookingTool.TheName,
        CompleteBookingTool.TheName,
        CancelBookingTool.TheName,
        ChargeBookingBalanceTool.TheName,
        ListBookingDatesTool.TheName,
        SaveBookingDateTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(BookingsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_bookings.Invocations);
        Assert.Empty(_refunder.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageBookings(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != BookingsPermissions.ManageBookings.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(BookingsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(BookingsPermissions.ManageBookings.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_bookings.Invocations);
        Assert.Empty(_slotStore.Invocations);
    }

    [Fact]
    public async Task ListBookings_BuildsTheBookingsListQuery()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListBookingsTool.TheName, Build(), new() { ["status"] = "confirmed", ["fromDate"] = "2026-12-01" }));
        var wrong = await InvokeAsync(ListBookingsTool.TheName, Build(), new() { ["status"] = "held" });

        // Assert
        Assert.Equal(BookingStatus.Confirmed, _query.Status);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), _query.FromUtc);
        Assert.False(_query.Upcoming);
        Assert.Equal("1042", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("orderNumber").GetString());
        Assert.StartsWith("Nothing was listed: status must be", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBooking_ReturnsTheQuoteAndBalance_ButNotTheTravelersDetails()
    {
        // Act
        var result = await InvokeAsync(GetBookingTool.TheName, Build(), new() { ["bookingId"] = "booking-1" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.Equal(400m, json.GetProperty("cancellation").GetProperty("paid").GetDecimal());
        Assert.Equal(300m, json.GetProperty("balance").GetProperty("amount").GetDecimal());
        Assert.Equal(1, json.GetProperty("travelers").GetInt32());
        Assert.DoesNotContain("X1234567", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBooking_SaysWhenTheBookingIsNotFound()
    {
        // Act
        var result = await InvokeAsync(GetBookingTool.TheName, Build(), new() { ["bookingId"] = "missing" });

        // Assert
        Assert.StartsWith("Nothing was read: no booking has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteBooking_MarksAConfirmedBookingAsANoShow_AndRefusesAnotherStatus()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CompleteBookingTool.TheName, Build(), new() { ["bookingId"] = "booking-1", ["outcome"] = "noShow" }));
        var again = await InvokeAsync(CompleteBookingTool.TheName, Build(), new() { ["bookingId"] = "booking-1", ["outcome"] = "completed" });

        // Assert
        Assert.Equal("noShow", json.GetProperty("status").GetString());
        Assert.Equal(BookingStatus.NoShow, _booking.Status);
        Assert.Contains("Only a confirmed booking", again, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 100, "USD", "idempotencyKey")]
    [InlineData("key-1", 100, "EUR", "paid in USD")]
    [InlineData("key-1", 450, "USD", "only 400.00 USD was paid")]
    [InlineData("key-1", 400, "USD", "Set refundAll to true")]
    public async Task CancelBooking_RefusesWithoutTheMoneyGuardrails(string key, int amount, string currency, string expected)
    {
        // Arrange
        var arguments = new Dictionary<string, object> { ["bookingId"] = "booking-1", ["refundAmount"] = (decimal)amount, ["currency"] = currency };

        if (key is not null)
        {
            arguments["idempotencyKey"] = key;
        }

        // Act
        var result = await InvokeAsync(CancelBookingTool.TheName, Build(), arguments);

        // Assert
        Assert.StartsWith("Nothing was canceled", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Equal(BookingStatus.Confirmed, _booking.Status);
        VerifyNoRefund();
    }

    [Fact]
    public async Task CancelBooking_RefundsTheAmountGiven_Once()
    {
        // Arrange
        var services = Build();
        var arguments = new Dictionary<string, object> { ["bookingId"] = "booking-1", ["refundAmount"] = 150m, ["currency"] = "USD", ["idempotencyKey"] = "cancel-1" };

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CancelBookingTool.TheName, services, arguments));
        var repeated = await InvokeAsync(CancelBookingTool.TheName, services, arguments);

        // Assert
        Assert.True(json.GetProperty("canceled").GetBoolean());
        Assert.Equal(150m, json.GetProperty("refunded").GetDecimal());
        Assert.Equal(BookingStatus.Canceled, _booking.Status);
        Assert.Equal(0, _slots[0].Sold);
        Assert.StartsWith("Nothing was canceled", repeated, StringComparison.Ordinal);
        _refunder.Verify(refunder => refunder.RefundAsync(_order, 150m, It.IsAny<string>(), AgentToolTestContext.UserName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelBooking_WithZero_CancelsWithoutARefund()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CancelBookingTool.TheName, Build(), new() { ["bookingId"] = "booking-1", ["refundAmount"] = 0m, ["currency"] = "USD", ["idempotencyKey"] = "cancel-0" }));

        // Assert
        Assert.Equal(0m, json.GetProperty("refunded").GetDecimal());
        Assert.Equal(BookingStatus.Canceled, _booking.Status);
        VerifyNoRefund();
    }

    [Theory]
    [InlineData(250, "USD")]
    [InlineData(300, "EUR")]
    public async Task ChargeBookingBalance_RefusesAnAmountThatIsNotTheBalanceOwed(int amount, string currency)
    {
        // Act
        var result = await InvokeAsync(ChargeBookingBalanceTool.TheName, Build(), new() { ["bookingId"] = "booking-1", ["amount"] = (decimal)amount, ["currency"] = currency, ["idempotencyKey"] = "charge-1" });

        // Assert
        Assert.Contains("the balance owed on booking 'Coastal hike' is 300.00 USD", result, StringComparison.Ordinal);
        _transactions.Verify(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChargeBookingBalance_RefusesWhenNothingIsOwed()
    {
        // Arrange
        _balance.Status = BookingBalanceStatus.Paid;

        // Act
        var result = await InvokeAsync(ChargeBookingBalanceTool.TheName, Build(), new() { ["bookingId"] = "booking-1", ["amount"] = 300m, ["currency"] = "USD", ["idempotencyKey"] = "charge-1" });

        // Assert
        Assert.Contains("nothing is owed", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChargeBookingBalance_ReportsAFailedCharge_AndNeverChargesTwiceForAKey()
    {
        // Arrange
        var services = Build();
        var arguments = new Dictionary<string, object> { ["bookingId"] = "booking-1", ["amount"] = 300m, ["currency"] = "USD", ["idempotencyKey"] = "charge-1" };

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ChargeBookingBalanceTool.TheName, services, arguments));
        var repeated = await InvokeAsync(ChargeBookingBalanceTool.TheName, services, arguments);

        // Assert
        Assert.False(json.GetProperty("charged").GetBoolean());
        Assert.StartsWith("The card wasn't charged", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("Nothing was charged again", repeated, StringComparison.Ordinal);
        _transactions.Verify(manager => manager.FindByIdAsync("transaction-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListBookingDates_ReturnsCapacityHeldSoldAndRemaining()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListBookingDatesTool.TheName, Build(), new() { ["productId"] = "tour" }));
        var date = Assert.Single(json.GetProperty("items").EnumerateArray());

        // Assert
        Assert.Equal("2026-12-09", date.GetProperty("date").GetString());
        Assert.Equal(5, date.GetProperty("remaining").GetInt32());
        Assert.Equal("Coastal hike", date.GetProperty("product").GetString());
    }

    [Fact]
    public async Task SaveBookingDate_AddsADate_WithTheProductsCapacity()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveBookingDateTool.TheName, Build(), new() { ["productId"] = "tour", ["date"] = "2026-12-20", ["price"] = 95m }));

        // Assert
        Assert.Equal("added", json.GetProperty("saved").GetString());
        _slotStore.Verify(store => store.CreateAsync(It.Is<BookingSlot>(slot => slot.SlotKey == "2026-12-20" && slot.Capacity == 8 && slot.Price == 95m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveBookingDate_NeverLowersTheCapacityBelowWhatIsBooked()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveBookingDateTool.TheName, Build(), new() { ["dateId"] = "slot-1", ["capacity"] = 1, ["closed"] = true }));

        // Assert
        Assert.Equal(5, _slots[0].Capacity);
        Assert.True(_slots[0].Closed);
        Assert.Contains("already held or sold", json.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveBookingDate_RefusesADateThatIsThereAlready()
    {
        // Arrange
        _slotStore.Setup(store => store.FindAsync("tour", "2026-12-09", It.IsAny<CancellationToken>())).ReturnsAsync(_slots[0]);

        // Act
        var result = await InvokeAsync(SaveBookingDateTool.TheName, Build(), new() { ["productId"] = "tour", ["date"] = "2026-12-09" });

        // Assert
        Assert.StartsWith("Nothing was saved: That date is there already.", result, StringComparison.Ordinal);
        _slotStore.Verify(store => store.CreateAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyNoRefund()
        => _refunder.Verify(refunder => refunder.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ListBookingsTool.TheName => [],
            CompleteBookingTool.TheName => new() { ["bookingId"] = "booking-1", ["outcome"] = "completed" },
            CancelBookingTool.TheName => new() { ["bookingId"] = "booking-1", ["refundAmount"] = 0m, ["currency"] = "USD", ["idempotencyKey"] = "key-1" },
            ChargeBookingBalanceTool.TheName => new() { ["bookingId"] = "booking-1", ["amount"] = 300m, ["currency"] = "USD", ["idempotencyKey"] = "key-1" },
            ListBookingDatesTool.TheName => new() { ["productId"] = "tour" },
            SaveBookingDateTool.TheName => new() { ["dateId"] = "slot-1", ["capacity"] = 12 },
            _ => new() { ["bookingId"] = "booking-1" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        var locks = new FakeDistributedLock();
        var session = Mock.Of<YesSqlSession>();
        var capacity = new BookingCapacityService(_slotStore.Object, _bookings.Object, session, locks, siteService.Object, clock.Object, NullLogger<BookingCapacityService>.Instance);
        var emails = new BookingEmailSender(Mock.Of<ICommerceEmailSender>(), siteService.Object, HtmlEncoder.Default, new PassThroughStringLocalizer<BookingEmailSender>());

        services.AddSingleton(_bookings.Object);
        services.AddSingleton(_slotStore.Object);
        services.AddSingleton(_balances.Object);
        services.AddSingleton(_orders.Object);
        services.AddSingleton(_contentManager.Object);
        services.AddSingleton(clock.Object);
        services.AddSingleton(session);
        services.AddSingleton(capacity);
        services.AddSingleton<IDistributedCache>(_cache);
        services.AddSingleton<IDistributedLock>(locks);
        services.AddSingleton(Create<BookingService>(
            _bookings.Object,
            _balances.Object,
            capacity,
            _orders.Object,
            _orderService.Object,
            emails,
            clock.Object,
            NullLogger<BookingService>.Instance,
            new PassThroughStringLocalizer<BookingService>(),
            _refunder.Object,
            _transactions.Object,
            Array.Empty<IBookingPaymentHandler>()));
        services.AddSingleton(Create<BookingBalanceCharger>(
            _balances.Object,
            _bookings.Object,
            _transactions.Object,
            clock.Object,
            NullLogger<BookingBalanceCharger>.Instance,
            new PassThroughStringLocalizer<BookingBalanceCharger>(),
            Array.Empty<IBookingPaymentHandler>()));
        services.AddSingleton(new BookingDates(_slotStore.Object, capacity, clock.Object, new PassThroughStringLocalizer<BookingDates>()));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListBookingsTool.TheName => new ListBookingsTool(),
            GetBookingTool.TheName => new GetBookingTool(),
            CompleteBookingTool.TheName => new CompleteBookingTool(),
            CancelBookingTool.TheName => new CancelBookingTool(),
            ChargeBookingBalanceTool.TheName => new ChargeBookingBalanceTool(),
            ListBookingDatesTool.TheName => new ListBookingDatesTool(),
            _ => new SaveBookingDateTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    // Built from the services it's given, so a dependency these paths don't use is passed as null.
    private static T Create<T>(params object[] services)
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => services.FirstOrDefault(service => service is not null && parameter.ParameterType.IsInstanceOfType(service)))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }
}
