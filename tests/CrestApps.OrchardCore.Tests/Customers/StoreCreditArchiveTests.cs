using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Indexes;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A balance with a long history never grows one ever larger document: store credit keeps its latest changes and older
/// ones move to their own indexed records, and finding a change by its reference still works for every change, recent or
/// moved out, with a keyed lookup.
/// </summary>
public sealed class StoreCreditArchiveTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-credit-archive-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new StoreCreditAccountIndexProvider()], CustomersConstants.CollectionName);
        _store.RegisterIndexes(new BalanceLedgerEntryIndexProvider());

        var cancellationToken = TestContext.Current.CancellationToken;

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CustomersConstants.CollectionName, cancellationToken);

        await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var schema = new SchemaBuilder(_store.Configuration, transaction);

        var credit = new StoreCreditMigrations { SchemaBuilder = schema };
        await credit.CreateAsync();
        await credit.UpdateFrom1Async();

        var tenders = new CheckoutTenderMigrations { SchemaBuilder = schema };
        await tenders.CreateAsync();
        await tenders.UpdateFrom1Async();

        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheAccount_KeepsItsLatestChanges_AndOlderOnesMoveToRecords_StillFoundByReference()
    {
        // Arrange: more changes than an account keeps.
        var holder = StoreCreditHolder.ForCustomer("customer-1");
        var changes = BalanceLedger.RecentEntryCount + 10;

        for (var index = 1; index <= changes; index++)
        {
            var result = await ChangeAsync(holder, 1m, $"Transaction:tx-{index}:pay-1");
            Assert.True(result.Succeeded);
        }

        // Act: repeat one change that moved out and one the account still keeps.
        var oldRepeat = await ChangeAsync(holder, 1m, "Transaction:tx-3:pay-1");
        var recentRepeat = await ChangeAsync(holder, 1m, $"Transaction:tx-{changes}:pay-1");

        // Assert
        Assert.True(oldRepeat.AlreadyApplied);
        Assert.True(recentRepeat.AlreadyApplied);

        await using var session = _store.CreateSession();
        var account = await new StoreCreditStore(session, new TestClock(_now), new BalanceLedgerArchive(session)).FindAsync(holder, "USD", TestContext.Current.CancellationToken);

        Assert.Equal(changes, account.Balance);
        Assert.Equal(BalanceLedger.RecentEntryCount, account.Activity.Count);
        Assert.Equal($"Transaction:tx-{changes}:pay-1", account.Activity[^1].Reference);

        var archived = await new BalanceLedgerArchive(session).ListAsync(StoreCreditStore.Ledger, account.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal(10, archived.Count);
        Assert.Equal("Transaction:tx-1:pay-1", archived[0].Reference);
    }

    private async Task<CustomerCreditResult> ChangeAsync(StoreCreditHolder holder, decimal amount, string reference)
    {
        await using var session = _store.CreateSession();
        var archive = new BalanceLedgerArchive(session);
        var clock = new TestClock(_now);
        var service = new StoreCreditService(new StoreCreditStore(session, clock, archive), session, new LocalLock(NullLogger<LocalLock>.Instance), clock, archive);

        return await service.TryChangeAsync(holder, "USD", amount, "Test", null, reference, TestContext.Current.CancellationToken);
    }
}
