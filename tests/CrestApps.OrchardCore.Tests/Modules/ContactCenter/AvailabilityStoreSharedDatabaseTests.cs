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

public sealed class AvailabilityStoreSharedDatabaseTests
{
    private static readonly DateTime _now = new(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AgentSessionStore_ListByUserIdsAsync_WhenMoreThanBatchSize_ReturnsEveryMatchingSession()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-availability-sessions-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);
        var userIds = Enumerable.Range(0, 501).Select(index => $"user-{index:D3}").ToArray();

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                foreach (var userId in userIds)
                {
                    await seedSession.SaveAsync(
                        new AgentSession
                        {
                            ItemId = $"session-{userId}",
                            UserId = userId,
                            IsOnline = true,
                            LastHeartbeatUtc = _now,
                            CreatedUtc = _now,
                        },
                        collection: ContactCenterConstants.CollectionName,
                        cancellationToken: TestContext.Current.CancellationToken);
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var sessionStore = new AgentSessionStore(querySession);

            // Act
            var sessions = await sessionStore.ListByUserIdsAsync(userIds, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(userIds.Length, sessions.Count);
            Assert.Contains(sessions, session => session.UserId == userIds[0]);
            Assert.Contains(sessions, session => session.UserId == userIds[userIds.Length - 1]);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InteractionStore_AvailabilityQueries_FilterStatusesWrapUpsAndBatchedAgentIds()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-availability-interactions-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);
        var agentIds = Enumerable.Range(0, 501).Select(index => $"agent-{index:D3}").ToArray();

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveInteractionAsync(seedSession, "active-first", agentIds[0], InteractionStatus.Connected);
                await SaveInteractionAsync(seedSession, "active-last", agentIds[agentIds.Length - 1], InteractionStatus.Ringing);
                await SaveInteractionAsync(seedSession, "created", agentIds[0], InteractionStatus.Created);
                await SaveInteractionAsync(seedSession, "failed", agentIds[0], InteractionStatus.Failed);
                await SaveInteractionAsync(
                    seedSession,
                    "pending-wrap-up",
                    agentIds[0],
                    InteractionStatus.Ended,
                    wrapUpStartedUtc: _now.AddMinutes(-5));
                await SaveInteractionAsync(
                    seedSession,
                    "completed-wrap-up",
                    agentIds[0],
                    InteractionStatus.Ended,
                    wrapUpStartedUtc: _now.AddMinutes(-10),
                    wrapUpCompletedUtc: _now.AddMinutes(-2));
                await SaveInteractionAsync(
                    seedSession,
                    "other-agent-wrap-up",
                    agentIds[1],
                    InteractionStatus.Ended,
                    wrapUpStartedUtc: _now.AddMinutes(-5));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var interactionStore = new InteractionStore(querySession);

            // Act
            var activeCounts = await interactionStore.CountActiveByAgentIdsAsync(
                agentIds,
                TestContext.Current.CancellationToken);
            var pendingWrapUps = await interactionStore.ListPendingWrapUpsByAgentAsync(
                agentIds[0],
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, activeCounts[agentIds[0]]);
            Assert.Equal(1, activeCounts[agentIds[agentIds.Length - 1]]);
            var pendingWrapUp = Assert.Single(pendingWrapUps);
            Assert.Equal("pending-wrap-up", pendingWrapUp.ItemId);
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
            new AgentSessionIndexProvider(),
            new InteractionIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<AgentSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("IsOnline")
            .Column<DateTime>("LastHeartbeatUtc"),
            collection: ContactCenterConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("Direction", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc")
            .Column<DateTime>("WrapUpStartedUtc")
            .Column<DateTime>("WrapUpCompletedUtc"),
            collection: ContactCenterConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SaveInteractionAsync(
        ISession session,
        string itemId,
        string agentId,
        InteractionStatus status,
        DateTime? wrapUpStartedUtc = null,
        DateTime? wrapUpCompletedUtc = null)
    {
        await session.SaveAsync(
            new Interaction
            {
                ItemId = itemId,
                AgentId = agentId,
                Status = status,
                CreatedUtc = _now,
                WrapUpStartedUtc = wrapUpStartedUtc,
                WrapUpCompletedUtc = wrapUpCompletedUtc,
            },
            collection: ContactCenterConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
