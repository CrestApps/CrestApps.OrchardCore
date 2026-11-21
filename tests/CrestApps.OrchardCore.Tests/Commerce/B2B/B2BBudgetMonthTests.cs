using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// The budget month Mild (fix verification): the budget counted the account's orders by the month they were made in, so
/// a checkout opened on the last day of a month and paid after midnight counted toward neither month. A payment hold
/// counts by when it was made and a paid order by when it was paid, in the month of the site's budget (UTC). The orders
/// are in a real SQLite store.
/// </summary>
public sealed class B2BBudgetMonthTests : IAsyncLifetime
{
    private const string AccountId = "account-1";

    private static readonly DateTime _now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _lastDayOfOctober = new(2026, 10, 31, 23, 50, 0, DateTimeKind.Utc);
    private static readonly DateTime _afterMidnight = new(2026, 11, 1, 0, 10, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-b2b-budget-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new OrderIndexProvider()], OrderConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OrderConstants.CollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await new OrderMigrations(steps: null) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnOrderMadeLastMonthAndPaidThisMonth_CountsThisMonth()
    {
        // Arrange
        await AddAsync(new Order { ItemId = "paid-after-midnight", Status = OrderStatus.Paid, Total = 40m, CreatedUtc = _lastDayOfOctober, PaidUtc = _afterMidnight });

        // Act
        var spent = await GetSpentThisMonthAsync();

        // Assert
        Assert.Equal(40m, spent);
    }

    [Fact]
    public async Task AnOrderMadeLastMonthWhosePaymentBeganThisMonth_CountsThisMonth()
    {
        // Arrange
        await AddAsync(new Order
        {
            ItemId = "held-after-midnight",
            Status = OrderStatus.AwaitingPayment,
            Subtotal = 30m,
            CreatedUtc = _lastDayOfOctober,
            BudgetHold = new OrderBudgetHold { Amount = 30m, Currency = "USD", HeldUtc = _afterMidnight },
        });

        // Act
        var spent = await GetSpentThisMonthAsync();

        // Assert
        Assert.Equal(30m, spent);
    }

    [Fact]
    public async Task OrdersPaidOrHeldLastMonth_DontCountThisMonth()
    {
        // Arrange: made this month's first minutes are no excuse; what counts is when it was paid or held.
        await AddAsync(new Order { ItemId = "paid-in-october", Status = OrderStatus.Paid, Total = 40m, CreatedUtc = _lastDayOfOctober.AddDays(-2), PaidUtc = _lastDayOfOctober });
        await AddAsync(new Order
        {
            ItemId = "held-in-october",
            Status = OrderStatus.AwaitingPayment,
            Subtotal = 30m,
            CreatedUtc = _lastDayOfOctober.AddMinutes(-5),
            BudgetHold = new OrderBudgetHold { Amount = 30m, Currency = "USD", HeldUtc = _lastDayOfOctober },
        });
        await AddAsync(new Order { ItemId = "paid-this-month", Status = OrderStatus.Paid, Total = 25m, CreatedUtc = _now.AddDays(-1), PaidUtc = _now.AddDays(-1) });

        // Act
        var spent = await GetSpentThisMonthAsync();

        // Assert
        Assert.Equal(25m, spent);
    }

    /// <summary>
    /// G16 (fix verification): the unpaid orders were read newest made first, 1,000 at most, from all time, so an order made
    /// last month and held this month fell off the page behind 1,000 newer open checkouts. Only unpaid orders asked for or
    /// held this month are read now.
    /// </summary>
    [Fact]
    public async Task AnOrderMadeLastMonthAndHeldThisMonth_CountsBehindMoreThanAPageOfNewerUnpaidOrders()
    {
        // Arrange: the older order held this month, then a page and more of open checkouts made after it.
        await AddAsync(new Order
        {
            ItemId = "held-after-midnight",
            Status = OrderStatus.AwaitingPayment,
            Subtotal = 30m,
            CreatedUtc = _lastDayOfOctober,
            BudgetHold = new OrderBudgetHold { Amount = 30m, Currency = "USD", HeldUtc = _afterMidnight },
        });

        await using (var session = _store.CreateSession())
        {
            var orders = new OrderStore(session);

            for (var i = 0; i < 1001; i++)
            {
                await orders.CreateAsync(new Order { ItemId = $"open-{i}", AccountId = AccountId, Currency = "USD", Status = OrderStatus.Draft, Subtotal = 5m, CreatedUtc = _now.AddMinutes(-i) }, TestContext.Current.CancellationToken);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var spent = await GetSpentThisMonthAsync();

        // Assert
        Assert.Equal(30m, spent);
    }

    /// <summary>
    /// G16: the orders migration that adds the budget column indexes the unpaid orders saved before it again, so an order
    /// held this month before the upgrade still counts.
    /// </summary>
    [Fact]
    public async Task AnUnpaidOrderIndexedBeforeTheBudgetColumn_CountsOnceTheUpgradeIndexesItAgain()
    {
        // Arrange
        await AddAsync(new Order
        {
            ItemId = "held-after-midnight",
            Status = OrderStatus.AwaitingPayment,
            Subtotal = 30m,
            CreatedUtc = _lastDayOfOctober,
            BudgetHold = new OrderBudgetHold { Amount = 30m, Currency = "USD", HeldUtc = _afterMidnight },
        });

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var dialect = _store.Configuration.SqlDialect;
            await using var command = transaction.Connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = $"UPDATE {dialect.QuoteForTableName(OrderConstants.CollectionName + "_" + nameof(OrderIndex), _store.Configuration.Schema)} SET {dialect.QuoteForColumnName(nameof(OrderIndex.BudgetCommittedUtc))} = NULL";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0m, await GetSpentThisMonthAsync());

        // Act: the deferred task the upgrade schedules.
        await using (var session = _store.CreateSession())
        {
            Assert.Equal(1, await OrderMigrations.ReindexUnpaidAsync(session, NullLogger.Instance));
        }

        // Assert
        Assert.Equal(30m, await GetSpentThisMonthAsync());
    }

    /// <summary>
    /// The orders migration adds the paid column without reading every order, so an order indexed before it has no paid
    /// time and counts by when it was made, as before, until it is saved again.
    /// </summary>
    [Fact]
    public async Task PaidOrdersIndexedBeforeThePaidColumn_CountByWhenTheyWereMade()
    {
        // Arrange
        await AddAsync(new Order { ItemId = "made-this-month", Status = OrderStatus.Paid, Total = 40m, CreatedUtc = _now.AddDays(-1), PaidUtc = _now.AddDays(-1) });
        await AddAsync(new Order { ItemId = "made-last-month", Status = OrderStatus.Paid, Total = 30m, CreatedUtc = _lastDayOfOctober, PaidUtc = _afterMidnight });

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var dialect = _store.Configuration.SqlDialect;
            await using var command = transaction.Connection.CreateCommand();

            command.Transaction = transaction;
            command.CommandText = $"UPDATE {dialect.QuoteForTableName(OrderConstants.CollectionName + "_" + nameof(OrderIndex), _store.Configuration.Schema)} SET {dialect.QuoteForColumnName(nameof(OrderIndex.PaidUtc))} = NULL";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var spent = await GetSpentThisMonthAsync();

        // Assert
        Assert.Equal(40m, spent);
    }

    private async Task AddAsync(Order order)
    {
        order.AccountId = AccountId;
        order.Currency = "USD";
        order.Subtotal = order.Subtotal == 0m ? order.Total ?? 0m : order.Subtotal;

        await using var session = _store.CreateSession();
        await new OrderStore(session).CreateAsync(order, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<decimal> GetSpentThisMonthAsync()
    {
        await using var session = _store.CreateSession();

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var approvals = new B2BApprovalService(
            new OrderStore(session),
            Mock.Of<IOrderService>(),
            siteService.Object,
            CommerceEmailSenders.Create(new ServiceCollection().BuildServiceProvider()),
            Mock.Of<IClock>(clock => clock.UtcNow == _now),
            new PassThroughStringLocalizer<B2BApprovalService>(),
            Mock.Of<IShopperCurrency>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<LinkGenerator>());

        return await approvals.GetSpentThisMonthAsync(TestB2BAccounts.Create(id: AccountId), "USD", cancellationToken: TestContext.Current.CancellationToken);
    }
}
