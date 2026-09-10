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
/// Integration coverage for the entitlement-based queue-membership lookup used by routed SMS: it must return
/// every agent entitled to (a member of) the queue, independent of voice presence or sign-in, in contrast to the
/// sign-in-gated <see cref="AgentQueueMembershipIndex"/>.
/// </summary>
public sealed class AgentAllowedQueueStoreTests
{
    private const string TargetQueueId = "queue-sales";
    private const string OtherQueueId = "queue-support";

    [Fact]
    public async Task GetMembersForQueueAsync_ReturnsEntitledMembers_RegardlessOfPresenceOrSignIn()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-allowed-queue-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                // Entitled (allowed) but NOT signed in and Away — must still be a member for SMS.
                await SaveAgentAsync(seedSession, "allowed-away", AgentPresenceStatus.Away, signedIn: [], allowed: [TargetQueueId]);
                // Signed in to the queue (also counts as a member).
                await SaveAgentAsync(seedSession, "signed-in", AgentPresenceStatus.Available, signedIn: [TargetQueueId], allowed: []);
                // Member of a different queue only — excluded.
                await SaveAgentAsync(seedSession, "other-queue", AgentPresenceStatus.Available, signedIn: [OtherQueueId], allowed: [OtherQueueId]);
                // No memberships at all — excluded.
                await SaveAgentAsync(seedSession, "no-queues", AgentPresenceStatus.Available, signedIn: [], allowed: []);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var agentStore = new AgentProfileStore(querySession);

            var members = await agentStore.GetMembersForQueueAsync(TargetQueueId, TestContext.Current.CancellationToken);

            var ids = members.Select(m => m.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Assert.Equal(["allowed-away", "signed-in"], ids);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetMembersForQueueAsync_MatchesCaseInsensitively()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cc-allowed-queue-case-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveAgentAsync(seedSession, "mixed-case", AgentPresenceStatus.Away, signedIn: [], allowed: ["Queue-Sales"]);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var agentStore = new AgentProfileStore(querySession);

            var members = await agentStore.GetMembersForQueueAsync("queue-sales", TestContext.Current.CancellationToken);

            var agent = Assert.Single(members);
            Assert.Equal("mixed-case", agent.ItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(
        [
            new AgentProfileIndexProvider(),
            new AgentAllowedQueueIndexProvider(),
        ]);
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

        await schemaBuilder.CreateMapIndexTableAsync<AgentAllowedQueueIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26)),
            collection: ContactCenterStorage.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveAgentAsync(
        ISession session,
        string itemId,
        AgentPresenceStatus presenceStatus,
        IList<string> signedIn,
        IList<string> allowed)
    {
        await session.SaveAsync(
            new AgentProfile
            {
                ItemId = itemId,
                Name = itemId,
                UserId = itemId,
                PresenceStatus = presenceStatus,
                QueueIds = signedIn,
                AllowedQueueIds = allowed,
                MaxConcurrentInteractions = 1,
            },
            collection: ContactCenterStorage.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
