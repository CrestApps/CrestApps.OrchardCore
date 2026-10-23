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
/// The agent workspace asks for an agent's newest interactions on every poll. The ask used to order the records through
/// the index, which grouped every interaction the agent ever handled before it kept the newest few; it now reads the
/// newest rows from the index and only then the records those rows name. These tests hold the answer to the same
/// records in the same order.
/// </summary>
public sealed class InteractionStoreRecentByAgentTests
{
    private static readonly DateTime _nowUtc = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetRecentByAgentAsync_ReturnsTheAgentsNewestInteractionsNewestFirst_AndNoOneElses()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"interaction-recent-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var session = store.CreateSession())
            {
                // Saved out of order, so the order of the answer can only come from CreatedUtc.
                await SaveAsync(session, "agent-a-3", "agent-a", _nowUtc.AddMinutes(-30));
                await SaveAsync(session, "agent-a-1", "agent-a", _nowUtc.AddMinutes(-10));
                await SaveAsync(session, "agent-b-1", "agent-b", _nowUtc.AddMinutes(-1));
                await SaveAsync(session, "agent-a-4", "agent-a", _nowUtc.AddMinutes(-40));
                await SaveAsync(session, "agent-a-2", "agent-a", _nowUtc.AddMinutes(-20));

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var readSession = store.CreateSession();
            var recent = await new InteractionStore(readSession).GetRecentByAgentAsync("agent-a", 3, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["agent-a-1", "agent-a-2", "agent-a-3"], recent.Select(interaction => interaction.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetRecentByAgentAsync_ReturnsNothing_ForAnAgentWithNoInteractions()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"interaction-recent-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();

            // Act
            var recent = await new InteractionStore(session).GetRecentByAgentAsync("agent-none", 10, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(recent);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Task SaveAsync(ISession session, string itemId, string agentId, DateTime createdUtc)
        => session.SaveAsync(
            new Interaction
            {
                ItemId = itemId,
                AgentId = agentId,
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
                CreatedUtc = createdUtc,
            },
            collection: ContactCenterStorage.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes([new InteractionIndexProvider()]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var migration = new InteractionIndexMigrations(store)
        {
            SchemaBuilder = new SchemaBuilder(store.Configuration, transaction),
        };

        await migration.CreateAsync();
        await migration.UpdateFrom1Async();
        await migration.UpdateFrom2Async();
        await migration.UpdateFrom3Async();
        await migration.UpdateFrom4Async();
        await migration.UpdateFrom5Async();
        await migration.UpdateFrom10Async();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
