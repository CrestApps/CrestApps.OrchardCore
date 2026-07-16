using System.Data.Common;
using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using Microsoft.Data.Sqlite;
using OrchardCore;
using OrchardCore.Data;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class OmnichannelActivityStoreTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListBulkManageableAsync_ReturnsOnlyEditableInventory()
    {
        // Arrange
        var databasePath = DatabasePath("bulk-manage");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        var editableStatuses = new[]
        {
            ActivityStatus.NotStated,
            ActivityStatus.Scheduled,
            ActivityStatus.Pending,
            ActivityStatus.AwaitingAgentResponse,
            ActivityStatus.Failed,
            ActivityStatus.Cancelled,
        };

        try
        {
            var expectedItemIds = new List<string>();
            string rawStatusItemId;

            await using (var seedSession = store.CreateSession())
            {
                foreach (var status in editableStatuses)
                {
                    expectedItemIds.Add(await SaveActivityAsync(seedSession, status));
                }

                await SaveActivityAsync(seedSession, ActivityStatus.Completed);
                await SaveActivityAsync(seedSession, ActivityStatus.Purged);
                await SaveActivityAsync(seedSession, ActivityStatus.Reserved);
                await SaveActivityAsync(seedSession, ActivityStatus.Dialing);
                await SaveActivityAsync(seedSession, ActivityStatus.InProgress);
                await SaveActivityAsync(seedSession, ActivityStatus.AwaitingCustomerAnswer);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
                rawStatusItemId = expectedItemIds[0];
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var activities = await activityStore.ListBulkManageableAsync(
                new BulkManageActivityFilter(),
                TestContext.Current.CancellationToken);
            var rawStatus = await ReadRawStatusAsync(
                store,
                connectionString,
                rawStatusItemId);

            // Assert
            // YesSql persists the ActivityStatus enum index column as its underlying integer value,
            // so raw-SQL predicates in OmnichannelActivityStore must compare against integer literals
            // (not enum names). This assertion guards that persistence contract.
            Assert.Equal(((int)ActivityStatus.NotStated).ToString(CultureInfo.InvariantCulture), rawStatus);
            Assert.Equal(
                expectedItemIds.OrderBy(itemId => itemId, StringComparer.Ordinal),
                activities.Select(activity => activity.ItemId).OrderBy(itemId => itemId, StringComparer.Ordinal));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenFilteredByEnumFacets_ReturnsMatchingActivity()
    {
        // Arrange
        var databasePath = DatabasePath("bulk-manage-filters");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string expectedItemId;

            await using (var seedSession = store.CreateSession())
            {
                expectedItemId = await SaveActivityAsync(
                    seedSession,
                    ActivityStatus.Failed,
                    ActivityInteractionType.Manual,
                    ActivityUrgencyLevel.High,
                    ActivityAssignmentStatus.Available);
                await SaveActivityAsync(
                    seedSession,
                    ActivityStatus.Failed,
                    ActivityInteractionType.Automated,
                    ActivityUrgencyLevel.High,
                    ActivityAssignmentStatus.Available);
                await SaveActivityAsync(
                    seedSession,
                    ActivityStatus.Failed,
                    ActivityInteractionType.Manual,
                    ActivityUrgencyLevel.Low,
                    ActivityAssignmentStatus.Available);
                await SaveActivityAsync(
                    seedSession,
                    ActivityStatus.Failed,
                    ActivityInteractionType.Manual,
                    ActivityUrgencyLevel.High,
                    ActivityAssignmentStatus.Assigned);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(
                querySession,
                store,
                connectionString,
                [new BulkManageActivityFilterHandler()]);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    Status = ActivityStatus.Failed,
                    InteractionType = ActivityInteractionType.Manual,
                    UrgencyLevel = ActivityUrgencyLevel.High,
                    AssignmentStatus = ActivityAssignmentStatus.Available,
                },
                TestContext.Current.CancellationToken);

            // Assert
            var activity = Assert.Single(result.Entries);
            Assert.Equal(expectedItemId, activity.ItemId);
            Assert.Equal(1, result.Count);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static OmnichannelActivityStore CreateActivityStore(
        ISession session,
        IStore store,
        string connectionString,
        IEnumerable<IBulkManageActivityFilterHandler> bulkManageHandlers = null)
    {
        return new OmnichannelActivityStore(
            session,
            [],
            bulkManageHandlers ?? [],
            store,
            new SqliteDbConnectionAccessor(connectionString));
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));
        store.RegisterIndexes([new OmnichannelActivityIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(50))
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<string>("AssignmentStatus", column => column.WithLength(50))
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("UrgencyLevel", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("InteractionType", column => column.WithLength(50)),
            collection: OmnichannelConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task<string> SaveActivityAsync(
        ISession session,
        ActivityStatus status,
        ActivityInteractionType interactionType = ActivityInteractionType.Manual,
        ActivityUrgencyLevel urgencyLevel = ActivityUrgencyLevel.Normal,
        ActivityAssignmentStatus assignmentStatus = ActivityAssignmentStatus.Unassigned)
    {
        var itemId = IdGenerator.GenerateId();

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = itemId,
                Channel = "Phone",
                ChannelEndpointId = "endpoint",
                ContactContentItemId = IdGenerator.GenerateId(),
                ContactContentType = "Lead",
                SubjectContentType = "LeadFollowUp",
                PreferredDestination = "+15555550100",
                ScheduledUtc = _now.AddMinutes((int)status),
                CreatedUtc = _now,
                InteractionType = interactionType,
                UrgencyLevel = urgencyLevel,
                AssignmentStatus = assignmentStatus,
                Status = status,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        return itemId;
    }

    private static async Task<string> ReadRawStatusAsync(
        IStore store,
        string connectionString,
        string itemId)
    {
        var tableName = store.Configuration.TableNameConvention.GetIndexTable(
            typeof(OmnichannelActivityIndex),
            OmnichannelConstants.CollectionName);
        var table = store.Configuration.SqlDialect.QuoteForTableName(
            $"{store.Configuration.TablePrefix}{tableName}",
            store.Configuration.Schema);
        var itemIdCol = store.Configuration.SqlDialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ItemId));
        var statusCol = store.Configuration.SqlDialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Status));

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {statusCol} FROM {table} WHERE {itemIdCol} = $itemId";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$itemId";
        parameter.Value = itemId;
        command.Parameters.Add(parameter);

        return (string)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            $"omnichannel-activity-store-{suffix}-{Guid.NewGuid():N}.db");
    }

    private sealed class SqliteDbConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;

        public SqliteDbConnectionAccessor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }
    }
}
