using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The inbox used to load every conversation in the tenant and filter it in memory. These tests pin the query
/// that replaced it: visibility, the tab and the page bound are all decided by the database, and a thread another
/// agent already owns is not offered to the rest of their department.
/// </summary>
public sealed class SmsInboxQueryTests
{
    private static readonly DateTime _now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueryAsync_ForAnAgent_ReturnsOwnedAssignedAndUnclaimedQueueThreads_ButNotAColleaguesQueueThread()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.SeedAsync(
            Personal("own-personal", ownerId: "agent-1"),
            Assigned("assigned-to-me", "agent-1"),
            Queue("queue-pooled", "queue-1", assignedAgentId: null),
            Queue("queue-taken-by-colleague", "queue-1", assignedAgentId: "agent-2"),
            Queue("other-department", "queue-9", assignedAgentId: null),
            Personal("someone-elses", ownerId: "agent-2"));

        var results = await harness.Store.QueryAsync(
            new MessagingInboxQuery
            {
                AgentId = "agent-1",
                QueueIds = ["queue-1"],
                Take = 50,
            },
            TestContext.Current.CancellationToken);

        var ids = results.Select(conversation => conversation.ItemId).ToArray();

        Assert.Contains("own-personal", ids);
        Assert.Contains("assigned-to-me", ids);
        Assert.Contains("queue-pooled", ids);
        Assert.DoesNotContain("queue-taken-by-colleague", ids);
        Assert.DoesNotContain("other-department", ids);
        Assert.DoesNotContain("someone-elses", ids);
    }

    [Fact]
    public async Task QueryAsync_ForASupervisor_ReturnsEveryThread()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.SeedAsync(
            Personal("own-personal", ownerId: "agent-1"),
            Personal("someone-elses", ownerId: "agent-2"),
            Queue("other-department", "queue-9", assignedAgentId: "agent-3"));

        var results = await harness.Store.QueryAsync(
            new MessagingInboxQuery { IncludeAll = true, Take = 50 },
            TestContext.Current.CancellationToken);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task QueryAsync_WithNoAgentIdentity_ReturnsNothing()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.SeedAsync(Personal("own-personal", ownerId: "agent-1"));

        var results = await harness.Store.QueryAsync(
            new MessagingInboxQuery { Take = 50 },
            TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_PagesInMostRecentFirstOrder_WithoutOverlapOrGaps()
    {
        await using var harness = await Harness.CreateAsync();

        var seeded = Enumerable.Range(0, 25)
            .Select(index =>
            {
                var conversation = Personal($"conv-{index:D2}", ownerId: "agent-1");
                conversation.LastMessageUtc = _now.AddMinutes(index);

                return conversation;
            })
            .ToArray();

        await harness.SeedAsync(seeded);

        var query = new MessagingInboxQuery { AgentId = "agent-1", Take = 10 };

        var first = await harness.Store.QueryAsync(query, TestContext.Current.CancellationToken);

        query.Skip = 10;
        var second = await harness.Store.QueryAsync(query, TestContext.Current.CancellationToken);

        query.Skip = 20;
        var third = await harness.Store.QueryAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(10, first.Count);
        Assert.Equal(10, second.Count);
        Assert.Equal(5, third.Count);

        // Newest first, and every page disjoint: the whole set is covered exactly once.
        Assert.Equal("conv-24", first[0].ItemId);
        Assert.Equal("conv-00", third[third.Count - 1].ItemId);

        var all = first.Concat(second).Concat(third).Select(conversation => conversation.ItemId).ToArray();

        Assert.Equal(25, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task CountAsync_CountsTheSameSetTheTabShows()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.SeedAsync(
            Assigned("assigned-to-me", "agent-1"),
            Assigned("also-mine", "agent-1"),
            Queue("queue-pooled", "queue-1", assignedAgentId: null));

        var cancellationToken = TestContext.Current.CancellationToken;

        var mine = await harness.Store.CountAsync(
            new MessagingInboxQuery { AgentId = "agent-1", QueueIds = ["queue-1"], Filter = MessagingInboxFilter.Mine },
            cancellationToken);
        var unassigned = await harness.Store.CountAsync(
            new MessagingInboxQuery { AgentId = "agent-1", QueueIds = ["queue-1"], Filter = MessagingInboxFilter.Unassigned },
            cancellationToken);
        var all = await harness.Store.CountAsync(
            new MessagingInboxQuery { AgentId = "agent-1", QueueIds = ["queue-1"] },
            cancellationToken);

        Assert.Equal(2, mine);
        Assert.Equal(1, unassigned);
        Assert.Equal(3, all);
    }

    [Fact]
    public async Task GetRoutedAwaitingPickupAsync_ReturnsOnlyThreadsStillAwaitingPickup()
    {
        await using var harness = await Harness.CreateAsync();

        var awaiting = Queue("awaiting", "queue-1", assignedAgentId: "agent-1");
        awaiting.AssignmentStatus = ConversationAssignmentStatus.Assigned;
        awaiting.AssignedUtc = _now;

        var pickedUp = Queue("picked-up", "queue-1", assignedAgentId: "agent-1");
        pickedUp.AssignmentStatus = ConversationAssignmentStatus.Assigned;
        pickedUp.AssignedUtc = null;

        await harness.SeedAsync(awaiting, pickedUp);

        var results = await harness.Store.GetRoutedAwaitingPickupAsync(TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("awaiting", results.First().ItemId);
    }

    [Fact]
    public async Task CountOpenAssignedAsync_CountsOnlyOpenAssignedThreads_ForTheAgentsAsked()
    {
        // Arrange
        // Routing reads this to decide who is least busy. It has to count what an agent is actually carrying:
        // closed threads and threads sitting in a pool are not work in front of anybody.
        await using var harness = await Harness.CreateAsync();

        var closed = Assigned("closed", "agent-1");
        closed.Status = ConversationStatus.Closed;

        await harness.SeedAsync(
            Assigned("open-1", "agent-1"),
            Assigned("open-2", "agent-1"),
            closed,
            Assigned("other", "agent-2"),
            Queue("pooled", "queue-1", assignedAgentId: null));

        // Act
        var counts = await harness.Store.CountOpenAssignedAsync(["agent-1", "agent-2", "agent-3"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, counts.GetValueOrDefault("agent-1"));
        Assert.Equal(1, counts.GetValueOrDefault("agent-2"));
        Assert.Equal(0, counts.GetValueOrDefault("agent-3"));
    }

    [Fact]
    public async Task CountOpenAssignedAsync_ForOneAgent_AgreesWithTheBatchedCount()
    {
        // Arrange
        // Two ways to ask the same question is two chances to answer it differently.
        await using var harness = await Harness.CreateAsync();

        await harness.SeedAsync(
            Assigned("open-1", "agent-1"),
            Assigned("open-2", "agent-1"));

        // Act
        var single = await harness.Store.CountOpenAssignedAsync("agent-1", TestContext.Current.CancellationToken);
        var batched = await harness.Store.CountOpenAssignedAsync(["agent-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, single);
        Assert.Equal(single, batched.GetValueOrDefault("agent-1"));
    }

    [Fact]
    public async Task CountOpenAssignedAsync_WithNobodyToCount_AsksNothing()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();

        // Act
        var counts = await harness.Store.CountOpenAssignedAsync([], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(counts);
    }

    [Fact]
    public async Task GetNextFirstResponseDueUtcAsync_ReturnsTheSoonestOpenTargetStillAhead()
    {
        // Arrange
        await using var harness = await Harness.CreateAsync();

        var overdue = Queue("overdue", "queue-1", assignedAgentId: null);
        overdue.FirstResponseDueUtc = _now.AddSeconds(-10);
        var later = Queue("later", "queue-1", assignedAgentId: null);
        later.FirstResponseDueUtc = _now.AddSeconds(40);
        var soonest = Queue("soonest", "queue-1", assignedAgentId: null);
        soonest.FirstResponseDueUtc = _now.AddSeconds(20);
        var closed = Queue("closed", "queue-1", assignedAgentId: null);
        closed.FirstResponseDueUtc = _now.AddSeconds(5);
        closed.Status = ConversationStatus.Closed;
        var replied = Queue("replied", "queue-1", assignedAgentId: null);

        await harness.SeedAsync(overdue, later, soonest, closed, replied);

        // Act
        var next = await harness.Store.GetNextFirstResponseDueUtcAsync(_now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now.AddSeconds(20), next);
    }

    private static MessagingConversation Personal(string itemId, string ownerId)
        => new()
        {
            ItemId = itemId,
            ServiceAddress = "+1555000" + itemId.GetHashCode(StringComparison.Ordinal).ToString("X4"),
            ContactAddress = "+1555111" + itemId.GetHashCode(StringComparison.Ordinal).ToString("X4"),
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = ownerId,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
            LastMessageUtc = _now,
            CreatedUtc = _now,
        };

    private static MessagingConversation Assigned(string itemId, string agentId)
    {
        var conversation = Personal(itemId, agentId);
        conversation.AssignedAgentId = agentId;
        conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;

        return conversation;
    }

    private static MessagingConversation Queue(string itemId, string queueId, string assignedAgentId)
    {
        var conversation = Personal(itemId, queueId);
        conversation.OwnerType = ConversationOwnerType.Queue;
        conversation.OwnerId = queueId;
        conversation.AssignedAgentId = assignedAgentId;
        conversation.AssignmentStatus = assignedAgentId is null
            ? ConversationAssignmentStatus.Pooled
            : ConversationAssignmentStatus.Assigned;

        return conversation;
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly ISession _session;
        private readonly string _databasePath;

        private Harness(IStore store, ISession session, string databasePath)
        {
            _store = store;
            _session = session;
            _databasePath = databasePath;
            Store = new MessagingConversationStore(session);
        }

        public MessagingConversationStore Store { get; }

        public static async Task<Harness> CreateAsync()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var databasePath = Path.Combine(Path.GetTempPath(), $"sms-inbox-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

            store.RegisterIndexes([new MessagingConversationIndexProvider()], MessagingStorage.CollectionName);
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(MessagingStorage.CollectionName, cancellationToken);

            await using (var migrationSession = store.CreateSession())
            {
                var transaction = await migrationSession.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await schemaBuilder.CreateMapIndexTableAsync<MessagingConversationIndex>(table => table
                    .Column<string>("ItemId", column => column.WithLength(26))
                    .Column<string>("Channel", column => column.WithLength(MessagingStorage.ChannelLength))
                    .Column<string>("ServiceAddress", column => column.WithLength(MessagingStorage.AddressLength))
                    .Column<string>("ContactAddress", column => column.WithLength(MessagingStorage.AddressLength))
                    .Column<string>("ContactContentItemId", column => column.WithLength(26))
                    .Column<string>("CustomerKey", column => column.WithLength(MessagingStorage.CustomerKeyLength))
                    .Column<string>("OwnerType", column => column.WithLength(32))
                    .Column<string>("OwnerId", column => column.WithLength(26))
                    .Column<string>("AssignedAgentId", column => column.WithLength(26))
                    .Column<string>("AssignmentStatus", column => column.WithLength(32))
                    .Column<string>("Status", column => column.WithLength(32))
                    .Column<bool>("IsRead")
                    .Column<DateTime>("LastMessageUtc")
                    .Column<int>("UnreadCount", column => column.NotNull().WithDefault(0))
                    .Column<DateTime>("AssignedUtc", column => column.Nullable())
                    .Column<DateTime>("FirstResponseDueUtc", column => column.Nullable()),
                    collection: MessagingStorage.CollectionName);

                await transaction.CommitAsync(cancellationToken);
            }

            return new Harness(store, store.CreateSession(), databasePath);
        }

        public async Task SeedAsync(params MessagingConversation[] conversations)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var seedSession = _store.CreateSession();
            var seedStore = new MessagingConversationStore(seedSession);

            foreach (var conversation in conversations)
            {
                conversation.ItemId ??= UniqueId.GenerateId();
                await seedStore.CreateAsync(conversation, cancellationToken);
            }

            await seedSession.SaveChangesAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _session.DisposeAsync();
            TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
        }
    }
}
