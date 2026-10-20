using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.BackgroundTasks;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Access and erasure requests, and the retention windows: personal data goes, the orders' lines and totals stay for
/// the store's accounts.
/// </summary>
public sealed class CustomerPrivacyTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCustomerStore _customers = new();
    private readonly InMemoryOrderStore _orders = new();
    private readonly InMemoryCheckoutSessionStore _sessions = new();

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private OrderAnonymizer CreateAnonymizer()
        => new(_orders, new ServiceCollection().BuildServiceProvider(), Clock());

    private OrderAnonymizer CreateAnonymizerWithCheckout()
        => new(_orders, new ServiceCollection()
            .AddSingleton<ICheckoutSessionAnonymizer>(new CheckoutSessionAnonymizer(_sessions, Clock()))
            .BuildServiceProvider(), Clock());

    private static CheckoutSession ASession(string sessionId, string orderId, string ownerId = null)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = orderId,
            OwnerId = ownerId,
            Status = CheckoutSessionStatus.Completed,
            IPAddress = "203.0.113.7",
            AgentInfo = "Mozilla/5.0",
            CreatedUtc = _now.AddMonths(-13),
            ModifiedUtc = _now.AddMonths(-13),
        };

        session.Put(new CheckoutContactInfo { DisplayName = "Pat Lee", Email = "pat@example.test", Phone = "+1 555 0100", BillingAddress = Home() });

        return session;
    }

    private static void AssertAnonymized(CheckoutSession session)
    {
        Assert.NotNull(session.AnonymizedUtc);
        Assert.Null(session.OwnerId);
        Assert.Null(session.IPAddress);
        Assert.Null(session.AgentInfo);
        Assert.True(session.TryGet<CheckoutContactInfo>(out var contact));
        Assert.Null(contact.DisplayName);
        Assert.Null(contact.Email);
        Assert.Null(contact.Phone);
        Assert.Null(contact.BillingAddress.AddressLine1);
    }

    private DefaultCustomerPrivacyService CreateService(params ICustomerPrivacyHandler[] handlers)
        => new(_customers, handlers, Clock());

    private DefaultCustomerPrivacyService CreateServiceWithOrders()
        => CreateService(new OrderCustomerPrivacyHandler(_orders, CreateAnonymizer(), new ServiceCollection().BuildServiceProvider()));

    private static Customer Pat(string id = "c1", string userId = null, DateTime? lastActivity = null)
        => new()
        {
            ItemId = id,
            UserId = userId,
            DisplayName = "Pat Lee",
            FirstName = "Pat",
            LastName = "Lee",
            CompanyName = "Contoso",
            TaxId = "VAT-1",
            Email = "pat@example.test",
            Phone = "+1 555 0100",
            AcceptsMarketing = true,
            Addresses = [new CustomerAddress { AddressId = "a1", Address = Home() }],
            DefaultBillingAddressId = "a1",
            ExternalIds = new(StringComparer.OrdinalIgnoreCase) { ["crm"] = "42" },
            LastActivityUtc = lastActivity ?? _now,
        };

    private static Address Home()
        => new() { Name = "Pat Lee", AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" };

    private static Order AnOrder(string id, string customerId = null, string userId = null, OrderStatus status = OrderStatus.Paid, DateTime? created = null)
        => new()
        {
            ItemId = id,
            OrderNumber = id.ToUpperInvariant(),
            CustomerId = customerId,
            UserId = userId,
            Status = status,
            Currency = "USD",
            Subtotal = 25m,
            Total = 25m,
            CustomerName = "Pat Lee",
            CustomerEmail = "pat@example.test",
            CustomerPhone = "+1 555 0100",
            BillingAddress = Home(),
            Lines = [new OrderLine { LineId = "l1", Title = "Mug", Quantity = 1, UnitPrice = 25m, LineTotal = 25m }],
            CreatedUtc = created ?? _now.AddDays(-1),
        };

    [Fact]
    public void AnonymizingACustomer_RemovesEverythingPersonal()
    {
        var customer = Pat(userId: "user-1");

        customer.Anonymize(_now);

        Assert.Equal(CustomerStatus.Anonymized, customer.Status);
        Assert.Equal(_now, customer.AnonymizedUtc);
        Assert.Null(customer.UserId);
        Assert.Null(customer.GetName());
        Assert.Null(customer.Email);
        Assert.Null(customer.Phone);
        Assert.Null(customer.TaxId);
        Assert.False(customer.AcceptsMarketing);
        Assert.Empty(customer.Addresses);
        Assert.Null(customer.DefaultBillingAddressId);
        Assert.Empty(customer.ExternalIds);
    }

    [Fact]
    public void AnonymizingAnOrder_KeepsTheLinesTotalsAndTaxLocation()
    {
        var order = AnOrder("o1");

        order.Anonymize(_now);

        Assert.Null(order.CustomerName);
        Assert.Null(order.CustomerEmail);
        Assert.Null(order.CustomerPhone);
        Assert.Equal("US", order.BillingAddress.Country);
        Assert.Equal("IL", order.BillingAddress.Region);
        Assert.Null(order.BillingAddress.AddressLine1);
        Assert.Null(order.BillingAddress.Name);
        Assert.Equal(25m, order.Total);
        Assert.Single(order.Lines);
        Assert.Equal(_now, order.AnonymizedUtc);
    }

    [Fact]
    public async Task Erasing_AnonymizesTheCustomerAndTheirOrders_AndRecordsWhoDidIt()
    {
        var customer = Pat(userId: "user-1");
        _customers.Customers.Add(customer);
        _orders.Orders.Add(AnOrder("o1", customerId: "c1", userId: "user-1"));

        // An account holder's order from before their customer record existed.
        _orders.Orders.Add(AnOrder("o2", userId: "user-1"));
        _orders.Orders.Add(AnOrder("o3", customerId: "someone-else"));

        await CreateServiceWithOrders().EraseAsync(customer, "admin", TestContext.Current.CancellationToken);

        Assert.Equal(CustomerStatus.Anonymized, customer.Status);
        Assert.All(_orders.Orders.Where(order => order.ItemId != "o3"), order => Assert.Null(order.CustomerEmail));
        Assert.Equal("pat@example.test", _orders.Orders.Single(order => order.ItemId == "o3").CustomerEmail);

        var entry = Assert.Single(customer.PrivacyLog);
        Assert.Equal(CustomerPrivacyAction.Erased, entry.Action);
        Assert.Equal("admin", entry.By);
        Assert.Equal(_now, entry.Utc);
    }

    [Fact]
    public async Task TheHandlers_SeeTheAccountBeforeItIsUnlinked_AndErasingTwiceDoesNothing()
    {
        var customer = Pat(userId: "user-1");
        var seen = new List<string>();
        var handler = new Mock<ICustomerPrivacyHandler>();
        handler
            .Setup(h => h.ErasingAsync(It.IsAny<CustomerPrivacyContext>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerPrivacyContext, CancellationToken>((context, _) => seen.Add(context.UserId))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler.Object);

        await service.EraseAsync(customer, "admin", TestContext.Current.CancellationToken);
        await service.EraseAsync(customer, "admin", TestContext.Current.CancellationToken);

        Assert.Equal(["user-1"], seen);
        Assert.Single(customer.PrivacyLog);
    }

    [Fact]
    public async Task Exporting_IncludesTheCustomerTheirAddressesAndOrders_AndRecordsWhoAsked()
    {
        var customer = Pat();
        _customers.Customers.Add(customer);
        _orders.Orders.Add(AnOrder("o1", customerId: "c1"));

        var export = await CreateServiceWithOrders().ExportAsync(customer, "admin", TestContext.Current.CancellationToken);

        Assert.Equal("pat@example.test", (string)export["customer"]["email"]);
        Assert.Equal("1 Main St", (string)export["addresses"][0]["address"]["addressLine1"]);
        Assert.Equal("O1", (string)export["orders"][0]["orderNumber"]);
        Assert.Equal("Mug", (string)export["orders"][0]["lines"][0]["title"]);

        var entry = Assert.Single(customer.PrivacyLog);
        Assert.Equal(CustomerPrivacyAction.Exported, entry.Action);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public async Task Retention_ErasesOnlyGuestsInactiveLongerThanTheWindow()
    {
        var stale = Pat("stale", lastActivity: _now.AddMonths(-25));
        var recent = Pat("recent", lastActivity: _now.AddMonths(-6));
        var account = Pat("account", userId: "user-1", lastActivity: _now.AddMonths(-36));
        _customers.Customers.AddRange([stale, recent, account]);

        var service = CreateServiceWithOrders();
        var settings = new CustomerPrivacySettings { InactiveGuestMonths = 24 };

        var erased = await CustomerRetentionBackgroundTask.ExpireGuestsAsync(settings, _now, _customers, service, TestContext.Current.CancellationToken);

        Assert.Equal(1, erased);
        Assert.Equal(CustomerStatus.Anonymized, stale.Status);
        Assert.Equal(CustomerPrivacyAction.Expired, Assert.Single(stale.PrivacyLog).Action);
        Assert.Null(Assert.Single(stale.PrivacyLog).By);
        Assert.Equal(CustomerStatus.Active, recent.Status);
        Assert.Equal(CustomerStatus.Active, account.Status);

        // Zero keeps everyone.
        Assert.Equal(0, await CustomerRetentionBackgroundTask.ExpireGuestsAsync(new CustomerPrivacySettings { InactiveGuestMonths = 0 }, _now, _customers, service, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Retention_AnonymizesOldSettledGuestOrders_NotAccountsOrUnpaidOnes()
    {
        _orders.Orders.AddRange(
        [
            AnOrder("old-guest", customerId: "g1", created: _now.AddMonths(-13)),
            AnOrder("old-canceled", customerId: "g1", status: OrderStatus.Canceled, created: _now.AddMonths(-13)),
            AnOrder("old-unpaid", customerId: "g1", status: OrderStatus.AwaitingPayment, created: _now.AddMonths(-13)),
            AnOrder("old-account", customerId: "c1", userId: "user-1", created: _now.AddMonths(-13)),
            AnOrder("new-guest", customerId: "g1", created: _now.AddMonths(-2)),
        ]);

        var anonymized = await CustomerRetentionBackgroundTask.AnonymizeGuestOrdersAsync(
            new CustomerPrivacySettings { GuestOrderMonths = 12 }, _now, CreateAnonymizer(), TestContext.Current.CancellationToken);

        Assert.Equal(2, anonymized);
        Assert.Equal(["old-canceled", "old-guest"], _orders.Orders.Where(order => order.AnonymizedUtc is not null).Select(order => order.ItemId).Order());

        // Already anonymized orders are not visited again.
        Assert.Equal(0, await CustomerRetentionBackgroundTask.AnonymizeGuestOrdersAsync(
            new CustomerPrivacySettings { GuestOrderMonths = 12 }, _now, CreateAnonymizer(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Erasing_AnonymizesEveryCheckoutAttemptBehindTheOrders_NotOtherCheckouts()
    {
        var customer = Pat(userId: "user-1");
        _customers.Customers.Add(customer);

        var order = AnOrder("o1", customerId: "c1", userId: "user-1");
        order.CheckoutSessionId = "s-paid";
        _orders.Orders.Add(order);

        _sessions.Seed(ASession("s-abandoned", "o1", ownerId: "user-1"));
        _sessions.Seed(ASession("s-paid", "o1", ownerId: "user-1"));
        _sessions.Seed(ASession("s-other", "o2", ownerId: "user-2"));

        var service = CreateService(new OrderCustomerPrivacyHandler(_orders, CreateAnonymizerWithCheckout(), new ServiceCollection().BuildServiceProvider()));

        await service.EraseAsync(customer, "admin", TestContext.Current.CancellationToken);

        AssertAnonymized(await _sessions.GetAsync("s-abandoned", TestContext.Current.CancellationToken));
        AssertAnonymized(await _sessions.GetAsync("s-paid", TestContext.Current.CancellationToken));

        var other = await _sessions.GetAsync("s-other", TestContext.Current.CancellationToken);
        Assert.Null(other.AnonymizedUtc);
        Assert.Equal("user-2", other.OwnerId);
        Assert.Equal("203.0.113.7", other.IPAddress);
    }

    [Fact]
    public async Task Retention_AnonymizesTheCheckoutsBehindOldGuestOrders()
    {
        var order = AnOrder("old-guest", customerId: "g1", created: _now.AddMonths(-13));
        order.CheckoutSessionId = "s-paid";
        _orders.Orders.Add(order);

        _sessions.Seed(ASession("s-first-try", "old-guest"));
        _sessions.Seed(ASession("s-paid", "old-guest"));

        var anonymized = await CustomerRetentionBackgroundTask.AnonymizeGuestOrdersAsync(
            new CustomerPrivacySettings { GuestOrderMonths = 12 }, _now, CreateAnonymizerWithCheckout(), TestContext.Current.CancellationToken);

        Assert.Equal(1, anonymized);
        AssertAnonymized(await _sessions.GetAsync("s-first-try", TestContext.Current.CancellationToken));
        AssertAnonymized(await _sessions.GetAsync("s-paid", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheHandlers_RunInTheirOrder_ThenInRegistrationOrder()
    {
        var customer = Pat();
        var ran = new List<string>();

        var service = CreateService(
            new RecordingHandler("late", 10, ran),
            new RecordingHandler("first-registered", 0, ran),
            new RecordingHandler("early", -100, ran),
            new RecordingHandler("second-registered", 0, ran));

        await service.EraseAsync(customer, "admin", TestContext.Current.CancellationToken);

        Assert.Equal(["early", "first-registered", "second-registered", "late"], ran);
    }

    [Fact]
    public void TheCommerceHandler_RunsBeforeTheOrderHandler()
    {
        // The commerce handler follows checkout emails and owners to guest transactions and plans, which the order
        // handler erases.
        ICustomerPrivacyHandler commerce = new CommercePersonalDataCustomerPrivacyHandler(Mock.Of<ICommercePersonalDataService>());
        ICustomerPrivacyHandler orders = new OrderCustomerPrivacyHandler(_orders, CreateAnonymizer(), new ServiceCollection().BuildServiceProvider());

        Assert.True(commerce.Order < orders.Order);
    }

    private sealed class RecordingHandler : ICustomerPrivacyHandler
    {
        private readonly string _name;
        private readonly List<string> _ran;

        public RecordingHandler(string name, int order, List<string> ran)
        {
            _name = name;
            Order = order;
            _ran = ran;
        }

        public int Order { get; }

        public Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default)
        {
            _ran.Add(_name);

            return Task.CompletedTask;
        }

        public Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task TheOrderStore_FindsOldGuestOrdersNotYetAnonymized()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-orders-{Guid.NewGuid():N}.db");
        var yesSql = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));

        try
        {
            yesSql.RegisterIndexes([new OrderIndexProvider()], OrderConstants.CollectionName);
            await yesSql.InitializeAsync(TestContext.Current.CancellationToken);
            await yesSql.InitializeCollectionAsync(OrderConstants.CollectionName, TestContext.Current.CancellationToken);

            await using (var session = yesSql.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var migrations = new OrderMigrations { SchemaBuilder = new SchemaBuilder(yesSql.Configuration, transaction) };
                await migrations.CreateAsync();
                await migrations.UpdateFrom4Async();
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var store = new OrderStore(session);
                var anonymized = AnOrder("anonymized", customerId: "g1", created: _now.AddMonths(-14));
                anonymized.Anonymize(_now);

                await store.CreateAsync(AnOrder("old-guest", customerId: "g1", created: _now.AddMonths(-13)), TestContext.Current.CancellationToken);
                await store.CreateAsync(AnOrder("old-account", userId: "user-1", created: _now.AddMonths(-13)), TestContext.Current.CancellationToken);
                await store.CreateAsync(AnOrder("new-guest", customerId: "g1", created: _now.AddMonths(-1)), TestContext.Current.CancellationToken);
                await store.CreateAsync(anonymized, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = yesSql.CreateSession())
            {
                var (_, orders) = await new OrderStore(session).PageAsync(new OrderQuery
                {
                    GuestsOnly = true,
                    CreatedBefore = _now.AddMonths(-12),
                    NotAnonymized = true,
                }, 0, 50, TestContext.Current.CancellationToken);

                Assert.Equal(["old-guest"], orders.Select(order => order.ItemId));
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(yesSql, path);
        }
    }
}
