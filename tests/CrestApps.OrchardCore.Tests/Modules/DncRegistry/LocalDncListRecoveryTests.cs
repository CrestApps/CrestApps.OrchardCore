using System.Text;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Migrations;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// Pins how the local DNC background task recovers lists that stopped. A delete issued while an import held
/// the list lock threw once from the after-request job and the scheduled task never looked at Deleting or
/// Failed lists, so those lists stayed stuck until someone edited the database.
/// </summary>
public sealed class LocalDncListRecoveryTests
{
    private const string ListId = "localdnclist0000000000002";
    private const string StoredFileName = ListId + ".csv";

    private static readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ScheduledRun_StaleDeletingList_IsDeleted()
    {
        await using var fixture = await Fixture.CreateAsync("1\r\n");
        await fixture.SeedListAsync(LocalDncListStatus.Deleting, processSaveUtc: _now.AddHours(-1));
        await fixture.SeedEntriesAsync(1_203);

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(await fixture.GetListAsync());
        Assert.Equal(0, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task ScheduledRun_RecentlyDeletingList_IsLeftToTheRunningDelete()
    {
        await using var fixture = await Fixture.CreateAsync("1\r\n");
        await fixture.SeedListAsync(LocalDncListStatus.Deleting, processSaveUtc: _now.AddMinutes(-2));
        await fixture.SeedEntriesAsync(3);

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LocalDncListStatus.Deleting, (await fixture.GetListAsync()).Status);
        Assert.Equal(3, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task Delete_WhileLockIsHeld_DoesNotThrowAndStaysDeleting()
    {
        await using var fixture = await Fixture.CreateAsync("1\r\n", lockAvailable: false);
        await fixture.SeedListAsync(LocalDncListStatus.Deleting, processSaveUtc: _now);

        await fixture.CreateManager().DeleteAsync(ListId, TestContext.Current.CancellationToken);

        Assert.Equal(LocalDncListStatus.Deleting, (await fixture.GetListAsync()).Status);
    }

    [Fact]
    public async Task ScheduledRun_FailedListWithProgress_ResumesWithoutDuplicates()
    {
        // Rows 1-3 were imported before the failure; rows 4-5 remain.
        var csv = "2065550100\r\n2065550101\r\n2065550102\r\n2065550103\r\n2065550104\r\n";
        await using var fixture = await Fixture.CreateAsync(csv);
        await fixture.SeedListAsync(
            LocalDncListStatus.Failed,
            processSaveUtc: _now.AddMinutes(-30),
            totalProcessed: 3,
            error: "Execution Timeout Expired.");
        await fixture.SeedEntriesAsync(["+12065550100", "+12065550101", "+12065550102"]);

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        var list = await fixture.GetListAsync();
        Assert.Equal(LocalDncListStatus.Completed, list.Status);
        Assert.Null(list.Error);
        Assert.Equal(0, list.FailedAttempts);
        Assert.Equal(5, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task ScheduledRun_FailedListOutOfAttempts_IsNotRetried()
    {
        await using var fixture = await Fixture.CreateAsync("2065550100\r\n");
        await fixture.SeedListAsync(
            LocalDncListStatus.Failed,
            processSaveUtc: _now.AddDays(-1),
            failedAttempts: LocalDncListRecoveryPolicy.MaxAutomaticImportAttempts);

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LocalDncListStatus.Failed, (await fixture.GetListAsync()).Status);
        Assert.Equal(0, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task ScheduledRun_StalledProcessingList_Resumes()
    {
        var csv = "2065550100\r\n2065550101\r\n";
        await using var fixture = await Fixture.CreateAsync(csv);
        await fixture.SeedListAsync(LocalDncListStatus.Processing, processSaveUtc: _now.AddMinutes(-45), totalProcessed: 1);
        await fixture.SeedEntriesAsync(["+12065550100"]);

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LocalDncListStatus.Completed, (await fixture.GetListAsync()).Status);
        Assert.Equal(2, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task ScheduledRun_ActiveProcessingList_IsNotStartedTwice()
    {
        await using var fixture = await Fixture.CreateAsync("2065550100\r\n");
        await fixture.SeedListAsync(LocalDncListStatus.Processing, processSaveUtc: _now.AddMinutes(-1));

        await LocalDncImportBackgroundTask.ProcessEntriesAsync(fixture.Services, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LocalDncListStatus.Processing, (await fixture.GetListAsync()).Status);
        Assert.Equal(0, await fixture.CountEntriesAsync());
    }

    [Fact]
    public async Task Resume_LargeList_LoadsEveryExistingNumberAcrossPages()
    {
        // More saved entries than one lookup page, all repeated in the file: none may be re-imported.
        const int saved = 5_000 + 37;
        var numbers = Enumerable.Range(0, saved).Select(i => $"+1206{5_000_000 + i:D7}").ToArray();
        var csv = new StringBuilder();
        foreach (var number in numbers)
        {
            csv.Append(number).Append("\r\n");
        }

        csv.Append(numbers[^1]).Append("\r\n");

        await using var fixture = await Fixture.CreateAsync(csv.ToString());
        await fixture.SeedListAsync(LocalDncListStatus.Paused, processSaveUtc: _now, totalProcessed: saved);
        await fixture.SeedEntriesAsync(numbers);

        await fixture.CreateManager().ProcessImportAsync(ListId, TestContext.Current.CancellationToken);

        var list = await fixture.GetListAsync();
        Assert.Equal(LocalDncListStatus.Completed, list.Status);
        Assert.Equal(saved, await fixture.CountEntriesAsync());
        Assert.Single(list.ErrorMessages);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly ServiceProvider _provider;
        private AsyncServiceScope _scope;

        private Fixture(string databasePath, IStore store, ServiceProvider provider)
        {
            _databasePath = databasePath;
            Store = store;
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }

        public IStore Store { get; }

        public IServiceProvider Services => _scope.ServiceProvider;

        public static async Task<Fixture> CreateAsync(string csv, bool lockAvailable = true)
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"dnc-local-recovery-{Guid.NewGuid():N}.db");
            var store = await CreateStoreAsync(databasePath);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() => lockAvailable ? (Mock.Of<ILocker>(), true) : (null, false));

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var services = new ServiceCollection();
            services.AddSingleton(store);
            services.AddScoped(sp => sp.GetRequiredService<IStore>().CreateSession());
            services.AddSingleton(distributedLock.Object);
            services.AddSingleton(clock.Object);
            services.AddSingleton(CreateFileStore(csv));
            services.AddSingleton<IPhoneNumberService, DefaultPhoneNumberService>();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddScoped<ILocalDncListManager, DefaultLocalDncListManager>();

            return new Fixture(databasePath, store, services.BuildServiceProvider());
        }

        public DefaultLocalDncListManager CreateManager()
            => (DefaultLocalDncListManager)Services.GetRequiredService<ILocalDncListManager>();

        public async Task SeedListAsync(
            LocalDncListStatus status,
            DateTime? processSaveUtc,
            int totalProcessed = 0,
            string error = null,
            int failedAttempts = 0)
        {
            await using var session = Store.CreateSession();
            await session.SaveAsync(
                new LocalDncList
                {
                    ListId = ListId,
                    CountryCode = "US",
                    Name = "Opt-outs",
                    UploadedFileName = "opt-outs.csv",
                    StoredFileName = StoredFileName,
                    ErrorMessages = [],
                    Status = status,
                    Error = error,
                    FailedAttempts = failedAttempts,
                    TotalProcessed = totalProcessed,
                    ImportedCount = totalProcessed,
                    PhoneNumberCount = totalProcessed,
                    CreatedUtc = _now.AddHours(-3),
                    ProcessSaveUtc = processSaveUtc,
                },
                false,
                DncRegistryConstants.CollectionName,
                TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public Task SeedEntriesAsync(int count)
            => SeedEntriesAsync(Enumerable.Range(0, count).Select(i => $"+1425{5_000_000 + i:D7}").ToArray());

        public async Task SeedEntriesAsync(string[] numbers)
        {
            await using var session = Store.CreateSession();
            foreach (var number in numbers)
            {
                await session.SaveAsync(
                    new LocalDncEntry { EntryId = Guid.NewGuid().ToString("N"), ListId = ListId, CountryCode = "US", PhoneNumber = number },
                    false,
                    DncRegistryConstants.CollectionName,
                    TestContext.Current.CancellationToken);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public async Task<LocalDncList> GetListAsync()
        {
            await using var session = Store.CreateSession();

            return await session.Query<LocalDncList, LocalDncListIndex>(i => i.ListId == ListId, collection: DncRegistryConstants.CollectionName)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        }

        public async Task<int> CountEntriesAsync()
        {
            await using var session = Store.CreateSession();

            return await session.QueryIndex<LocalDncEntryIndex>(i => i.ListId == ListId, collection: DncRegistryConstants.CollectionName)
                .CountAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);
        }

        private static ILocalDncFileStore CreateFileStore(string csv)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);

            var fileInfo = new Mock<IFileStoreEntry>();
            fileInfo.SetupGet(entry => entry.Path).Returns(StoredFileName);
            fileInfo.SetupGet(entry => entry.Name).Returns(StoredFileName);
            fileInfo.SetupGet(entry => entry.Length).Returns(bytes.Length);

            var fileStore = new Mock<ILocalDncFileStore>();
            fileStore.Setup(s => s.GetFileInfoAsync(StoredFileName)).ReturnsAsync(fileInfo.Object);
            fileStore.Setup(s => s.GetFileStreamAsync(It.IsAny<IFileStoreEntry>())).ReturnsAsync(() => new MemoryStream(bytes, writable: false));

            return fileStore.Object;
        }

        private static async Task<IStore> CreateStoreAsync(string databasePath)
        {
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
            store.RegisterIndexes([new LocalDncListIndexProvider(), new LocalDncEntryIndexProvider()]);
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
}
