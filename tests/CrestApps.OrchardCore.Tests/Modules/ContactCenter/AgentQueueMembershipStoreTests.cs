using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentQueueMembershipStoreTests
{
    private const string TargetQueueId = "queue-sales";
    private const string OtherQueueId = "queue-support";

    [Fact]
    public async Task ListAvailableForQueueAsync_ReturnsOnlyEntitledSignedInAvailableAgents()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-membership-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAgentAsync(seedSession, "entitled", AgentPresenceStatus.Available, [TargetQueueId], [TargetQueueId]);
                await SaveAgentAsync(seedSession, "not-signed-in", AgentPresenceStatus.Available, [], [TargetQueueId]);
                await SaveAgentAsync(seedSession, "not-allowed", AgentPresenceStatus.Available, [TargetQueueId], []);
                await SaveAgentAsync(seedSession, "away", AgentPresenceStatus.Away, [TargetQueueId], [TargetQueueId]);
                await SaveAgentAsync(seedSession, "other-queue", AgentPresenceStatus.Available, [OtherQueueId], [OtherQueueId]);

                // Available agents entitled to a different queue must not be loaded by the target-queue query.
                for (var index = 0; index < 200; index++)
                {
                    await SaveAgentAsync(
                        seedSession,
                        $"noise-{index:D3}",
                        AgentPresenceStatus.Available,
                        [OtherQueueId],
                        [OtherQueueId]);
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var agentStore = new AgentProfileStore(querySession);

            // Act
            var available = await agentStore.ListAvailableForQueueAsync(
                TargetQueueId,
                TestContext.Current.CancellationToken);

            // Assert
            var agent = Assert.Single(available);
            Assert.Equal("entitled", agent.ItemId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ListAvailableForQueueAsync_MatchesQueueMembershipCaseInsensitively()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-membership-case-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAgentAsync(seedSession, "mixed-case", AgentPresenceStatus.Available, ["Queue-Sales"], ["Queue-Sales"]);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var agentStore = new AgentProfileStore(querySession);

            // Act
            var available = await agentStore.ListAvailableForQueueAsync(
                "queue-sales",
                TestContext.Current.CancellationToken);

            // Assert
            var agent = Assert.Single(available);
            Assert.Equal("mixed-case", agent.ItemId);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task AgentQueueMembershipIndex_EmitsOneNormalizedRowPerEntitledQueue()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-membership-index-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAgentAsync(
                    seedSession,
                    "multi",
                    AgentPresenceStatus.Available,
                    [TargetQueueId, OtherQueueId, "queue-unallowed"],
                    [TargetQueueId, OtherQueueId]);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();

            // Act
            var rows = await querySession
                .QueryIndex<AgentQueueMembershipIndex>(
                    index => index.ItemId == "multi",
                    collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            // Assert
            var queueIds = rows.Select(row => row.QueueId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert.Equal([TargetQueueId, OtherQueueId], queueIds);
            Assert.All(rows, row => Assert.Equal(AgentPresenceStatus.Available, row.PresenceStatus));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new AgentProfileIndexProvider(),
            new AgentQueueMembershipIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50))
            .Column<int>("MaxConcurrentInteractions"),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveAgentAsync(
        ISession session,
        string itemId,
        AgentPresenceStatus presenceStatus,
        IList<string> queueIds,
        IList<string> allowedQueueIds)
    {
        await session.SaveAsync(
            new AgentProfile
            {
                ItemId = itemId,
                Name = itemId,
                UserId = itemId,
                PresenceStatus = presenceStatus,
                QueueIds = queueIds,
                AllowedQueueIds = allowedQueueIds,
                MaxConcurrentInteractions = 1,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
