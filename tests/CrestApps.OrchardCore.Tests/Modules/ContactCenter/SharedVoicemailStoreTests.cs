using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The shared voicemail page's status filter reads straight from the index, so each filter is run against a real SQLite
/// store built by the module's own migration, over messages in every state across two queues.
/// </summary>
public sealed class SharedVoicemailStoreTests
{
    private static readonly DateTime _now = new(2026, 9, 26, 15, 0, 0, DateTimeKind.Utc);

    // The page opens on "Open": the messages still to be dealt with, whoever holds them.
    [Fact]
    public async Task QueryAsync_WithNoStatusAndNotIncludingResolved_ReadsTheNewAndClaimedMessages()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var page = await database.QueryAsync(new SharedVoicemailQuery());

        // Assert
        Assert.Equal(3, page.Count);
        Assert.Equal(["billing-new", "main-claimed", "main-new"], page.Entries.Select(voicemail => voicemail.ItemId));
    }

    [Fact]
    public async Task QueryAsync_ForResolved_ReadsOnlyTheResolvedMessages()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var page = await database.QueryAsync(new SharedVoicemailQuery { Status = SharedVoicemailStatus.Resolved });

        // Assert
        Assert.Equal(2, page.Count);
        Assert.Equal(["billing-resolved", "main-resolved"], page.Entries.Select(voicemail => voicemail.ItemId));
    }

    // "All" is every message, newest first, the ones already dealt with among them.
    [Fact]
    public async Task QueryAsync_IncludingResolved_ReadsEveryMessage_NewestFirst()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var page = await database.QueryAsync(new SharedVoicemailQuery { IncludeResolved = true });

        // Assert
        Assert.Equal(5, page.Count);
        Assert.Equal(
            ["billing-new", "billing-resolved", "main-claimed", "main-resolved", "main-new"],
            page.Entries.Select(voicemail => voicemail.ItemId));
    }

    [Fact]
    public async Task QueryAsync_ForOneQueue_ReadsNothingFromTheOther()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var page = await database.QueryAsync(new SharedVoicemailQuery { QueueIds = ["queue-main"], IncludeResolved = true });

        // Assert
        Assert.Equal(["main-claimed", "main-resolved", "main-new"], page.Entries.Select(voicemail => voicemail.ItemId));
    }

    // A user entitled to no queue asks with an empty list, which is not valid SQL as an IN clause: it reads nothing
    // rather than failing, and rather than reading every queue as no filter would.
    [Fact]
    public async Task QueryAsync_WithAnEmptyQueueList_ReadsAnEmptyPage()
    {
        // Arrange
        await using var database = await SeededDatabase.CreateAsync();

        // Act
        var page = await database.QueryAsync(new SharedVoicemailQuery { QueueIds = [], IncludeResolved = true });

        // Assert
        Assert.Equal(0, page.Count);
        Assert.Empty(page.Entries);
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
            var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-shared-voicemail-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
            var database = new SeededDatabase(store, databasePath);

            try
            {
                store.RegisterIndexes([new SharedVoicemailIndexProvider()]);
                await store.InitializeAsync(TestContext.Current.CancellationToken);
                await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

                await using (var session = store.CreateSession())
                {
                    var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                    var migration = new SharedVoicemailIndexMigrations
                    {
                        SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
                    };

                    await migration.CreateAsync();
                    await transaction.CommitAsync(TestContext.Current.CancellationToken);
                }

                // Received at distinct times, and not in the order they are saved, so newest-first is the query's doing.
                await using (var session = store.CreateSession())
                {
                    await SaveAsync(session, "main-new", "queue-main", SharedVoicemailStatus.New, minutesAgo: 50);
                    await SaveAsync(session, "main-resolved", "queue-main", SharedVoicemailStatus.Resolved, minutesAgo: 40);
                    await SaveAsync(session, "billing-new", "queue-billing", SharedVoicemailStatus.New, minutesAgo: 10);
                    await SaveAsync(session, "main-claimed", "queue-main", SharedVoicemailStatus.Claimed, minutesAgo: 30);
                    await SaveAsync(session, "billing-resolved", "queue-billing", SharedVoicemailStatus.Resolved, minutesAgo: 20);
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

        public async Task<SharedVoicemailPage> QueryAsync(SharedVoicemailQuery query)
        {
            await using var session = _store.CreateSession();

            return await new SharedVoicemailStore(session).QueryAsync(query, TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

            return ValueTask.CompletedTask;
        }

        private static async Task SaveAsync(ISession session, string itemId, string queueId, SharedVoicemailStatus status, int minutesAgo)
        {
            var receivedUtc = _now.AddMinutes(-minutesAgo);

            await session.SaveAsync(
                new SharedVoicemail
                {
                    ItemId = itemId,
                    InteractionId = $"interaction-{itemId}",
                    QueueId = queueId,
                    Status = status,
                    ReceivedUtc = receivedUtc,
                    ClaimedByUserId = status == SharedVoicemailStatus.New ? null : "user-1",
                    ResolvedUtc = status == SharedVoicemailStatus.Resolved ? receivedUtc.AddMinutes(5) : null,
                    CreatedUtc = receivedUtc,
                },
                collection: ContactCenterStorage.CollectionName,
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
