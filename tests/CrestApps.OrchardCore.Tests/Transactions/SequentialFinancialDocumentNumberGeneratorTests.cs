using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Models;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Locking;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Receipt numbers are short and in order, and a number is never issued twice. These run against a real store,
/// because what matters is what the next issuer reads back after the last one saved.
/// </summary>
public sealed class SequentialFinancialDocumentNumberGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_IssuesShortNumbersInOrderAndRemembersTheLastOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-numbers-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(path);
        var locks = new LocalLock(NullLogger<LocalLock>.Instance);

        try
        {
            var issued = new List<string>();

            // Each receipt is issued by its own unit of work, as each payment is.
            for (var i = 0; i < 3; i++)
            {
                await using var session = store.CreateSession();
                var generator = new SequentialFinancialDocumentNumberGenerator(session, locks);
                var number = await generator.GenerateAsync(new FinancialDocumentNumberRequest(FinancialDocumentKind.Receipt), TestContext.Current.CancellationToken);

                issued.Add(number.PublicToken);
            }

            Assert.Equal(["R-1001", "R-1002", "R-1003"], issued);

            await using (var session = store.CreateSession())
            {
                var generator = new SequentialFinancialDocumentNumberGenerator(session, locks);

                // Another kind keeps its own series, and the receipts series carries on where it was.
                var invoice = await generator.GenerateAsync(new FinancialDocumentNumberRequest(FinancialDocumentKind.Invoice), TestContext.Current.CancellationToken);

                Assert.Equal("INV-1001", invoice.PublicToken);
                Assert.Equal(SequentialFinancialDocumentNumberGenerator.FirstNumber, invoice.Sequence);
            }

            await using (var session = store.CreateSession())
            {
                var documents = await session.Query<FinancialDocumentSequences>(collection: TransactionsConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

                var only = Assert.Single(documents);

                Assert.Equal(1003, only.Values[nameof(FinancialDocumentKind.Receipt)]);
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

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(TransactionsConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }
}
