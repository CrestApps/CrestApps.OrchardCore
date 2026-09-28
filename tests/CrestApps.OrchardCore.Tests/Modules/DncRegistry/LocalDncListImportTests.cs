using System.Text;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Migrations;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// Pins how an uploaded local do-not-call list is imported. A leading "PhoneNumber" header used to be recorded as a
/// rejected row, so a file whose every number imported ended as completed with errors and the header showed up in
/// the error download.
/// </summary>
public sealed class LocalDncListImportTests
{
    private const string ListId = "localdnclist0000000000001";
    private const string StoredFileName = ListId + ".csv";

    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessImport_HeaderRow_IsNotAnError()
    {
        // Arrange
        const string csv = "PhoneNumber\r\n+14255551212\r\n(206) 555-0100\r\n2065550101\r\n";
        var databasePath = Path.Combine(Path.GetTempPath(), $"dnc-local-import-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await seedSession.SaveAsync(
                    new LocalDncList
                    {
                        ListId = ListId,
                        CountryCode = "US",
                        Name = "Internal opt-outs",
                        UploadedFileName = "opt-outs.csv",
                        StoredFileName = StoredFileName,
                        ErrorMessages = [],
                        Status = LocalDncListStatus.Pending,
                        CreatedUtc = _now,
                    },
                    false,
                    DncRegistryConstants.CollectionName,
                    TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var managerSession = store.CreateSession())
            {
                var manager = new DefaultLocalDncListManager(
                    managerSession,
                    Mock.Of<IDistributedLock>(),
                    CreateFileStore(csv),
                    CreateClock(),
                    new DefaultPhoneNumberService(),
                    NullLogger<DefaultLocalDncListManager>.Instance);

                // Act
                await manager.ProcessImportAsync(ListId, TestContext.Current.CancellationToken);
            }

            // Assert
            await using var readSession = store.CreateSession();
            var list = await readSession.Query<LocalDncList, LocalDncListIndex>(
                index => index.ListId == ListId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(list);
            Assert.Null(list.Error);
            Assert.Equal(LocalDncListStatus.Completed, list.Status);
            Assert.Empty(list.ErrorMessages ?? []);
            Assert.Equal(3, list.ImportedCount);
            Assert.Equal(3, list.PhoneNumberCount);

            var entries = await readSession.Query<LocalDncEntry, LocalDncEntryIndex>(
                index => index.ListId == ListId, collection: DncRegistryConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(
                ["+12065550100", "+12065550101", "+14255551212"],
                entries.Select(entry => entry.PhoneNumber).OrderBy(number => number, StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ILocalDncFileStore CreateFileStore(string csv)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);

        var fileInfo = new Mock<IFileStoreEntry>();
        fileInfo.SetupGet(entry => entry.Path).Returns(StoredFileName);
        fileInfo.SetupGet(entry => entry.Name).Returns(StoredFileName);
        fileInfo.SetupGet(entry => entry.Length).Returns(bytes.Length);

        var fileStore = new Mock<ILocalDncFileStore>();
        fileStore
            .Setup(store => store.GetFileInfoAsync(StoredFileName))
            .ReturnsAsync(fileInfo.Object);

        // The manager reads the file twice (once to count rows, once to import them), so each read gets its own stream.
        fileStore
            .Setup(store => store.GetFileStreamAsync(It.IsAny<IFileStoreEntry>()))
            .ReturnsAsync(() => new MemoryStream(bytes, writable: false));

        return fileStore.Object;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(clock => clock.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new LocalDncListIndexProvider(),
            new LocalDncEntryIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(DncRegistryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await new LocalDncRegistryMigrations(NullLogger<LocalDncRegistryMigrations>.Instance) { SchemaBuilder = schemaBuilder }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
