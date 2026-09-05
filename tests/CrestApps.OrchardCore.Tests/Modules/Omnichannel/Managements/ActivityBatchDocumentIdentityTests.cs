using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// A batch is one record that moves through its statuses, so loading it must leave exactly one document behind.
/// The loader saves the batch, flushes the session to release the memory the created activities hold, and then
/// saves the batch again to mark it Loaded. If the flush costs the session its tracking of the batch, that final
/// save inserts a second document with the same ItemId -- which is what put duplicate rows on the batches list,
/// one stuck at Loading and one at Loaded.
/// </summary>
public sealed class ActivityBatchDocumentIdentityTests
{
    [Fact]
    public async Task SavingABatchAfterFlushing_UpdatesTheSameDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-activity-batch-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            var batch = new OmnichannelActivityBatch
            {
                ItemId = "batch-1",
                DisplayText = "First batch",
                Source = "Dialer",
                Status = OmnichannelActivityBatchStatus.Loading,
                CreatedUtc = DateTime.UtcNow,
            };

            await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);

            // The loader creates the activities here, then flushes so their memory can be released.
            await session.FlushAsync(TestContext.Current.CancellationToken);

            // Act: the same batch instance is then marked Loaded and saved again.
            batch.Status = OmnichannelActivityBatchStatus.Loaded;

            await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await using var querySession = store.CreateSession();

            var batches = await querySession
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-1",
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            var stored = Assert.Single(batches);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, stored.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// Characterizes the hazard the loader has to work around: a commit -- not a flush -- costs the session its
    /// tracking of the batch, so saving the same instance afterwards inserts a second document rather than
    /// updating the first. This is how the store behaves, so the loader must not save a batch instance it held
    /// across a commit; the test below covers the pattern that does the right thing. The earlier tests here only
    /// ever flushed, which is why they cleared the loader of a defect it does have.
    /// </summary>
    [Fact]
    public async Task SavingABatchInstanceHeldAcrossACommit_InsertsASecondDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-activity-batch-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            var batch = new OmnichannelActivityBatch
            {
                ItemId = "batch-commit",
                DisplayText = "Committed batch",
                Source = "Dialer",
                Status = OmnichannelActivityBatchStatus.Loading,
                CreatedUtc = DateTime.UtcNow,
            };

            await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);

            // The loader commits partway through so a long load does not hold one transaction open for its
            // whole run.
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act: the same batch instance is then marked Loaded and saved again.
            batch.Status = OmnichannelActivityBatchStatus.Loaded;

            await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await using var querySession = store.CreateSession();

            var batches = await querySession
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-commit",
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            // Two documents for one batch: the duplicate rows on the batches list, one stuck at Loading and one
            // at Loaded.
            Assert.Equal(2, batches.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// The pattern the loader must use: re-read the batch after a commit so the session tracks the document it
    /// is about to update, instead of saving an instance whose identity the commit discarded.
    /// </summary>
    [Fact]
    public async Task SavingABatchReReadAfterCommitting_UpdatesTheSameDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-activity-batch-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            var batch = new OmnichannelActivityBatch
            {
                ItemId = "batch-reread",
                DisplayText = "Re-read batch",
                Source = "Dialer",
                Status = OmnichannelActivityBatchStatus.Loading,
                CreatedUtc = DateTime.UtcNow,
            };

            await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act: read the batch back before marking it Loaded, so the session knows the document.
            var tracked = await session
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-reread",
                    collection: OmnichannelConstants.CollectionName)
                .FirstOrDefaultAsync();

            tracked.Status = OmnichannelActivityBatchStatus.Loaded;

            await session.SaveAsync(tracked, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await using var querySession = store.CreateSession();

            var batches = await querySession
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-reread",
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            var stored = Assert.Single(batches);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, stored.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// The production sequence: the batch is read back through a query (as the coordinator does with
    /// FindByIdAsync) before it is mutated and saved, so the session knows it from the identity map rather than
    /// from having saved it.
    /// </summary>
    [Fact]
    public async Task SavingAQueriedBatchAfterFlushing_UpdatesTheSameDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-activity-batch-q-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seed = store.CreateSession())
            {
                await seed.SaveAsync(NewBatch(), collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                var batch = await session
                    .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                        index => index.ItemId == "batch-1",
                        collection: OmnichannelConstants.CollectionName)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

                batch.Status = OmnichannelActivityBatchStatus.Loading;
                batch.TotalLoaded = 1;
                await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);

                await session.FlushAsync(TestContext.Current.CancellationToken);

                batch.Status = OmnichannelActivityBatchStatus.Loaded;
                batch.TotalLoaded = 4;
                await session.SaveAsync(batch, collection: OmnichannelConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            await using var querySession = store.CreateSession();
            var batches = await querySession
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-1",
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            var stored = Assert.Single(batches);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, stored.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// The production sequence, driven through the real catalog the loader uses rather than through raw YesSql:
    /// read the batch, mark it Loading and save, flush the session the way the loader does after creating the
    /// activities, then mark it Loaded and save again. Exactly one document must survive.
    /// </summary>
    [Fact]
    public async Task UpdatingABatchThroughTheCatalogAfterFlushing_UpdatesTheSameDocument()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"omnichannel-activity-batch-c-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(store);
            services.AddScoped<ISession>(_ => session);
            services.AddYesSqlDocumentCatalog<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(collection: OmnichannelConstants.CollectionName);

            await using var provider = services.BuildServiceProvider();
            var catalog = provider.GetRequiredService<ICatalog<OmnichannelActivityBatch>>();

            var seeded = NewBatch();
            await catalog.CreateAsync(seeded, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            var batch = await catalog.FindByIdAsync("batch-1", TestContext.Current.CancellationToken);

            batch.Status = OmnichannelActivityBatchStatus.Loading;
            batch.TotalLoaded = 1;
            await catalog.UpdateAsync(batch, TestContext.Current.CancellationToken);

            // The loader flushes here to release the memory held by the activities it just created.
            await session.FlushAsync(TestContext.Current.CancellationToken);

            // Act
            batch.Status = OmnichannelActivityBatchStatus.Loaded;
            batch.TotalLoaded = 4;
            await catalog.UpdateAsync(batch, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Assert
            await using var querySession = store.CreateSession();
            var batches = await querySession
                .Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                    index => index.ItemId == "batch-1",
                    collection: OmnichannelConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            var stored = Assert.Single(batches);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, stored.Status);
            Assert.Equal(4, stored.TotalLoaded);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static OmnichannelActivityBatch NewBatch()
        => new()
        {
            ItemId = "batch-1",
            DisplayText = "First batch",
            Source = "Dialer",
            Status = OmnichannelActivityBatchStatus.Started,
            CreatedUtc = DateTime.UtcNow,
        };

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new OmnichannelActivityBatchIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(OmnichannelConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: OmnichannelConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
