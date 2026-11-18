using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Which agents can't take work now, from their presence on SQLite: an agent on a break, away or signed out can't; one
/// who is available, on a call or wrapping one up can; a user who isn't an agent isn't judged here.
/// </summary>
public sealed class AgentPresenceAvailabilitySourceTests
{
    [Fact]
    public async Task AgentsOnABreakAwayOrSignedOut_CantTakeWork_AndUsersWhoArentAgentsArentJudged()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-availability-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seed = store.CreateSession())
            {
                foreach (var (userId, status) in new[]
                {
                    ("on-break", AgentPresenceStatus.Break),
                    ("away", AgentPresenceStatus.Away),
                    ("offline", AgentPresenceStatus.Offline),
                    ("available", AgentPresenceStatus.Available),
                    ("on-call", AgentPresenceStatus.Busy),
                    ("wrapping-up", AgentPresenceStatus.WrapUp),
                })
                {
                    await seed.SaveAsync(new AgentProfile { ItemId = userId, Name = userId, UserId = userId, PresenceStatus = status, MaxConcurrentInteractions = 1 }, collection: ContactCenterStorage.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
                }

                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();
            var source = new AgentPresenceAvailabilitySource(session);

            // Act
            var unavailable = await source.GetUnavailableAsync(["on-break", "away", "offline", "available", "on-call", "wrapping-up", "not-an-agent"], TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["away", "offline", "on-break"], unavailable.Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new AgentProfileIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
