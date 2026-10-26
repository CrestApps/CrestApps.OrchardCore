using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Migrations;
using CrestApps.OrchardCore.Transactions.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// A module finds its own record from an installment plan, and from any payment of one, through indexed columns. These
/// run the queries against a real SQLite database, through the shipped migrations, because a missing column or a map
/// that skips a row is invisible to an in-memory fake.
/// </summary>
public sealed class InstallmentPlanReferencePersistenceTests
{
    private static readonly DateTime _now = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InstallmentPlanStore_FindsPlansByTheRecordTheyPayForAndTheTransactionTheyReplace()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-plan-reference-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await SeedPlansAsync(store);

            await using var session = store.CreateSession();
            var planStore = new InstallmentPlanStore(session, new TestClock(_now));

            var byReference = await planStore.GetByReferenceAsync("Registration", "reg-1", TestContext.Current.CancellationToken);
            var bySource = await planStore.GetBySourceTransactionAsync("tx-source", TestContext.Current.CancellationToken);
            var none = await planStore.GetByReferenceAsync("Registration", "reg-unknown", TestContext.Current.CancellationToken);

            Assert.Equal("plan-1", Assert.Single(byReference).ItemId);
            Assert.Equal("plan-1", Assert.Single(bySource).ItemId);
            Assert.Empty(none);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task TransactionStore_FindsEveryPaymentOfARecordByItsParentReference()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-parent-reference-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var seed = store.CreateSession())
            {
                var transactionStore = new TransactionStore(seed, new TestClock(_now));

                await transactionStore.CreateAsync(CreateTransaction("tx-1", "Registration", "reg-1", _now), TestContext.Current.CancellationToken);
                await transactionStore.CreateAsync(CreateTransaction("tx-2", "Registration", "reg-1", _now.AddMonths(1)), TestContext.Current.CancellationToken);
                await transactionStore.CreateAsync(CreateTransaction("tx-other", "Registration", "reg-2", _now), TestContext.Current.CancellationToken);

                // An ordinary transaction has no parent reference, and must still be saved.
                await transactionStore.CreateAsync(CreateTransaction("tx-plain", null, null, _now), TestContext.Current.CancellationToken);

                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();
            var reader = new TransactionStore(session, new TestClock(_now));

            var payments = await reader.GetByParentReferenceAsync("Registration", "reg-1", TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "tx-1", "tx-2" }, payments.Select(payment => payment.ItemId));
            Assert.NotNull(await reader.FindByIdAsync("tx-plain", TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task SeedPlansAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var planStore = new InstallmentPlanStore(session, new TestClock(_now));

        await planStore.CreateAsync(new InstallmentPlan
        {
            ItemId = "plan-1",
            Title = "Season fee",
            OwnerId = "customer-1",
            Currency = "USD",
            TotalAmount = 300m,
            ReferenceType = "Registration",
            ReferenceId = "reg-1",
            SourceTransactionId = "tx-source",
            Status = InstallmentPlanStatus.Active,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);

        await planStore.CreateAsync(new InstallmentPlan
        {
            ItemId = "plan-2",
            Title = "Kitchen remodel",
            OwnerId = "customer-2",
            Currency = "USD",
            TotalAmount = 1200m,
            Status = InstallmentPlanStatus.Active,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Transaction CreateTransaction(string id, string parentReferenceType, string parentReferenceId, DateTime dueUtc)
        => new()
        {
            ItemId = id,
            Title = id,
            Source = SubscriptionConstants.InstallmentPlans.TransactionSource,
            OwnerId = "customer-1",
            ReferenceType = SubscriptionConstants.InstallmentPlans.ReferenceType,
            ReferenceId = "plan-1",
            ParentReferenceType = parentReferenceType,
            ParentReferenceId = parentReferenceId,
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Pending,
            CreatedUtc = _now,
            UpdatedUtc = _now,
            DueUtc = dueUtc,
        };

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new InstallmentPlanIndexProvider(),
            new TransactionIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(SubscriptionConstants.InstallmentPlanCollectionName, TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(TransactionsConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        Assert.Equal(2, await new InstallmentPlanMigrations { SchemaBuilder = schemaBuilder }.CreateAsync());
        await new TransactionMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
