using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// M9 and M11 (final security review), run against a real SQLite database through the shipped migrations and the real
/// stores, because both are about which rows the queries find: a canceled agreement whose paid period ended was never
/// found by any sweep, so a sold site kept running unbilled; and paying the overdue invoice of a past-due agreement found
/// nothing to resume, so the grace window expired a customer who had paid.
/// </summary>
public sealed class SubscriptionLapseAndRecoverySqliteTests
{
    private static readonly DateTime _now = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// M9: a subscription canceled at period end is found once its period is over, and not before.
    /// </summary>
    [Fact]
    public async Task GetLapsedAsync_FindsACanceledSubscriptionOnlyOnceItsPaidPeriodEnded()
    {
        await using var database = await Database.CreateAsync();

        await using (var session = database.Store.CreateSession())
        {
            var store = new SubscriptionStore(session, new TestClock(_now));
            await store.CreateAsync(Canceled("ended", _now.AddDays(-1)), TestContext.Current.CancellationToken);
            await store.CreateAsync(Canceled("running", _now.AddDays(5)), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = database.Store.CreateSession())
        {
            var lapsed = await new SubscriptionStore(session, new TestClock(_now)).GetLapsedAsync(_now, TestContext.Current.CancellationToken);

            Assert.Equal(["ended"], lapsed.Select(subscription => subscription.ItemId));
        }
    }

    /// <summary>
    /// M11: paying the overdue renewal invoice of a past-due agreement returns it to active.
    /// </summary>
    [Fact]
    public async Task PayingTheOnlyOverdueInvoice_ResumesThePastDueSubscription()
    {
        await using var database = await Database.CreateAsync();
        await using var session = database.Store.CreateSession();
        var (handler, subscriptions, transactions) = await SeedPastDueAsync(session, overdueRenewals: 1);

        var invoice = await transactions.GetByObligationAsync("session-1", "plan-ob:cycle-2", TestContext.Current.CancellationToken);
        invoice.Status = TransactionStatus.Paid;
        await transactions.UpdateAsync(invoice, TestContext.Current.CancellationToken);

        await handler.StatusChangedAsync(invoice, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        var subscription = await subscriptions.FindByIdAsync("sub-1", TestContext.Current.CancellationToken);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.GraceEndsUtc);
    }

    /// <summary>
    /// M11: an agreement with another invoice still overdue stays past due when one of them is paid.
    /// </summary>
    [Fact]
    public async Task PayingOneOfTwoOverdueInvoices_LeavesTheSubscriptionPastDue()
    {
        await using var database = await Database.CreateAsync();
        await using var session = database.Store.CreateSession();
        var (handler, subscriptions, transactions) = await SeedPastDueAsync(session, overdueRenewals: 2);

        var invoice = await transactions.GetByObligationAsync("session-1", "plan-ob:cycle-2", TestContext.Current.CancellationToken);
        invoice.Status = TransactionStatus.Paid;
        await transactions.UpdateAsync(invoice, TestContext.Current.CancellationToken);

        await handler.StatusChangedAsync(invoice, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        Assert.Equal(SubscriptionStatus.PastDue, (await subscriptions.FindByIdAsync("sub-1", TestContext.Current.CancellationToken)).Status);
    }

    private static async Task<(SubscriptionFirstPaymentTransactionHandler Handler, SubscriptionManager Subscriptions, TransactionStore Transactions)> SeedPastDueAsync(
        ISession session,
        int overdueRenewals)
    {
        var clock = new TestClock(_now);
        var subscriptionStore = new SubscriptionStore(session, clock);
        var transactions = new TransactionStore(session, clock);
        var manager = new SubscriptionManager(subscriptionStore, [], NullLogger<CatalogManager<Subscription>>.Instance);

        await subscriptionStore.CreateAsync(new Subscription
        {
            ItemId = "sub-1",
            Title = "Site plan",
            OwnerId = "owner-1",
            ProviderKey = "PayLater",
            CheckoutSessionId = "session-1",
            ObligationId = "plan-ob",
            Status = SubscriptionStatus.PastDue,
            PastDueSinceUtc = _now.AddDays(-3),
            GraceEndsUtc = _now.AddDays(4),
            Currency = "USD",
            Amount = 20m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CyclesBilled = 1 + overdueRenewals,
            CurrentPeriodStartUtc = _now.AddDays(-10),
            CurrentPeriodEndUtc = _now.AddDays(20),
            CreatedUtc = _now.AddMonths(-3),
            UpdatedUtc = _now.AddDays(-3),
        }, TestContext.Current.CancellationToken);

        // Pay Later chains each cycle's obligation onto the one before it.
        var obligation = "plan-ob";
        await transactions.CreateAsync(Invoice("tx-1", obligation, 1, TransactionStatus.Paid, overdue: false), TestContext.Current.CancellationToken);

        for (var cycle = 2; cycle <= overdueRenewals + 1; cycle++)
        {
            obligation = $"{obligation}:cycle-{cycle}";
            await transactions.CreateAsync(Invoice($"tx-{cycle}", obligation, cycle, TransactionStatus.Outstanding, overdue: true), TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        var lifecycle = new DefaultSubscriptionLifecycleService(
            manager,
            new LocalLock(NullLogger<LocalLock>.Instance),
            SiteServiceFactory.Create(new SubscriptionSettings { DunningGraceDays = 7 }),
            clock,
            [],
            NullLogger<DefaultSubscriptionLifecycleService>.Instance);

        return (new SubscriptionFirstPaymentTransactionHandler(lifecycle, manager, transactions), manager, transactions);
    }

    private static Transaction Invoice(string itemId, string obligationId, int cycle, TransactionStatus status, bool overdue)
        => new()
        {
            ItemId = itemId,
            Title = "Site plan",
            CheckoutSessionId = "session-1",
            ObligationId = obligationId,
            Currency = "USD",
            Amount = 20m,
            TotalAmount = 20m,
            Status = status,
            DueUtc = _now.AddMonths(cycle - 4),
            BecameOverdueUtc = overdue ? _now.AddMonths(cycle - 4).AddDays(1) : null,
            CreatedUtc = _now.AddMonths(cycle - 4),
            UpdatedUtc = _now.AddMonths(cycle - 4),
            Recurrence = new TransactionRecurrence { CycleNumber = cycle, BillingDuration = 1, DurationType = DurationType.Month },
        };

    private static Subscription Canceled(string itemId, DateTime periodEnd)
        => new()
        {
            ItemId = itemId,
            Title = "Site plan",
            OwnerId = "owner-1",
            Status = SubscriptionStatus.Canceled,
            CancelAtPeriodEnd = true,
            CanceledUtc = _now.AddDays(-20),
            CurrentPeriodStartUtc = periodEnd.AddMonths(-1),
            CurrentPeriodEndUtc = periodEnd,
            CreatedUtc = _now.AddMonths(-2),
            UpdatedUtc = _now.AddDays(-20),
        };

    private sealed class Database : IAsyncDisposable
    {
        private readonly string _path;

        private Database(IStore store, string path)
        {
            Store = store;
            _path = path;
        }

        public IStore Store { get; }

        public static async Task<Database> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"crestapps-subscription-lapse-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));
            store.RegisterIndexes([new SubscriptionRecordIndexProvider(), new TransactionIndexProvider()]);
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(SubscriptionConstants.SubscriptionCollectionName, TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(TransactionsConstants.CollectionName, TestContext.Current.CancellationToken);

            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await new SubscriptionRecordMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new TransactionMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await transaction.CommitAsync(TestContext.Current.CancellationToken);

            return new Database(store, path);
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(Store, _path);

            return ValueTask.CompletedTask;
        }
    }
}
