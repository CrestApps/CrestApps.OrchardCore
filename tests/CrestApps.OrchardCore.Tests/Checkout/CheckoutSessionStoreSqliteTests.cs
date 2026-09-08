using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Runs the checkout session through a real YesSql store, because the defect this pins is invisible to an
/// in-memory fake: a session saved again after a commit was inserted as a second document instead of
/// updating the first, so a completed checkout existed twice — once pending and once completed.
/// </summary>
public sealed class CheckoutSessionStoreSqliteTests
{
    /// <summary>
    /// The engine commits a checkout's confirmed payment before fulfilling it, then commits the completion.
    /// Two commits of one session in one scope must leave exactly one document.
    /// </summary>
    [Fact]
    public async Task SavingASessionAfterACommit_UpdatesTheSameDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-checkout-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);

        try
        {
            await using (var session = store.CreateSession())
            {
                var checkout = new CheckoutSession
                {
                    SessionId = "session-1",
                    Status = CheckoutSessionStatus.PaymentPending,
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow,
                };

                await session.SaveAsync(checkout, cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);

                checkout.Status = CheckoutSessionStatus.Completed;

                await session.SaveAsync(checkout, cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var documents = await session.Query<CheckoutSession, CheckoutSessionIndex>(x => x.SessionId == "session-1").ListAsync(TestContext.Current.CancellationToken);

                var only = Assert.Single(documents);

                Assert.Equal(CheckoutSessionStatus.Completed, only.Status);
            }
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new CheckoutSessionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        var migration = new CheckoutMigrations { SchemaBuilder = schemaBuilder };
        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
