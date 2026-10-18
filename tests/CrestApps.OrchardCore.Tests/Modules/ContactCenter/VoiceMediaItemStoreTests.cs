using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An entry written before provider copies were tracked per provider recorded one provider and one reference. Every
/// way the store reads entries has to fold that pair into the map, or an export, a name lookup or a play would see an
/// entry with no copies at all.
/// </summary>
public sealed class VoiceMediaItemStoreTests
{
    [Fact]
    public async Task FindByIdAsync_FoldsTheSingleCopyOfAnOlderEntry()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var item = await database.ReadAsync(store => store.FindByIdAsync("legacy-1", TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Equal("telnyx-copy", Assert.Single(item.ProviderReferences).Value);
    }

    [Fact]
    public async Task FindByNameAsync_FoldsTheSingleCopyOfAnOlderEntry()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var item = await database.ReadAsync(store => store.FindByNameAsync("Legacy hold music", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("legacy-1", item.ItemId);
        Assert.Equal("telnyx-copy", Assert.Single(item.ProviderReferences).Value);
    }

    [Fact]
    public async Task GetAllAsync_FoldsTheSingleCopyOfAnOlderEntry_AndLeavesANewerEntryAlone()
    {
        // Arrange
        // A deployment export reads every entry this way; without the fold the plan carries the entry unfolded.
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var items = await database.ReadAsync(store => store.GetAllAsync(TestContext.Current.CancellationToken));

        // Assert
        var legacy = Assert.Single(items, item => item.ItemId == "legacy-1");
        var current = Assert.Single(items, item => item.ItemId == "current-1");
        Assert.Equal("telnyx-copy", Assert.Single(legacy.ProviderReferences).Value);
        Assert.Equal("current-copy", Assert.Single(current.ProviderReferences).Value);
    }

    private sealed class SeededDatabase : IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly string _databasePath;

        private SeededDatabase(IStore store, string databasePath)
        {
            _store = store;
            _databasePath = databasePath;
        }

        public static async Task<SeededDatabase> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-voice-media-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
            var database = new SeededDatabase(store, databasePath);

            try
            {
                store.RegisterIndexes([new VoiceMediaItemIndexProvider()]);
                await store.InitializeAsync(TestContext.Current.CancellationToken);
                await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

                await using (var session = store.CreateSession())
                {
                    var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                    var migration = new VoiceMediaItemIndexMigrations
                    {
                        SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
                    };

                    await migration.CreateAsync();
                    await transaction.CommitAsync(TestContext.Current.CancellationToken);
                }

                await using (var session = store.CreateSession())
                {
                    // Written as an earlier release wrote it: one provider, one reference, and no map at all.
                    await session.SaveAsync(
                        new VoiceMediaItem
                        {
                            ItemId = "legacy-1",
                            Name = "Legacy hold music",
                            ProviderName = "Telnyx",
                            MediaReference = "telnyx-copy",
                            Format = "mp3",
                            ProviderReferences = null,
                        },
                        collection: ContactCenterStorage.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);

                    await session.SaveAsync(
                        new VoiceMediaItem
                        {
                            ItemId = "current-1",
                            Name = "Current hold music",
                            MasterStorageReference = "voice-media-current",
                            ProviderName = "Telnyx",
                            MediaReference = "current-copy",
                            ProviderReferences = new Dictionary<string, string> { ["Telnyx"] = "current-copy" },
                        },
                        collection: ContactCenterStorage.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);

                    await session.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }
            catch
            {
                await database.DisposeAsync();

                throw;
            }

            return database;
        }

        public async Task<TResult> ReadAsync<TResult>(Func<VoiceMediaItemStore, Task<TResult>> read)
        {
            await using var session = _store.CreateSession();

            return await read(new VoiceMediaItemStore(session));
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

            return ValueTask.CompletedTask;
        }
    }
}
