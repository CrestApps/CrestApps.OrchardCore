using System.Globalization;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterClaimMigrationTests
{
    [Fact]
    public async Task ActivityReservationMigration_ValidLegacyRows_BackfillsUniqueClaims()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-reservation-migration-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyReservationIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ActivityReservationIndex>(store);
            await InsertReservationIndexAsync(
                schemaBuilder,
                tableName,
                1,
                "reservation-1",
                "activity-1",
                "agent-1",
                ReservationStatus.Pending);
            await InsertReservationIndexAsync(
                schemaBuilder,
                tableName,
                2,
                "reservation-2",
                "activity-2",
                "agent-1",
                ReservationStatus.Accepted);
            var migration = new ActivityReservationIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal(
                "activity-1",
                await ReadClaimAsync(schemaBuilder, tableName, "ActivityClaimKey", "reservation-1"));
            Assert.Equal(
                "agent-1",
                await ReadClaimAsync(schemaBuilder, tableName, "AgentClaimKey", "reservation-1"));
            Assert.Equal(
                "activity-2",
                await ReadClaimAsync(schemaBuilder, tableName, "ActivityClaimKey", "reservation-2"));
            Assert.Equal(
                "reservation-2",
                await ReadClaimAsync(schemaBuilder, tableName, "AgentClaimKey", "reservation-2"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ActivityReservationMigration_DuplicateLegacyActivityClaims_FailsWithRepairGuidance()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-reservation-duplicate-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyReservationIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ActivityReservationIndex>(store);
            await InsertReservationIndexAsync(
                schemaBuilder,
                tableName,
                1,
                "reservation-1",
                "activity-1",
                "agent-1",
                ReservationStatus.Pending);
            await InsertReservationIndexAsync(
                schemaBuilder,
                tableName,
                2,
                "reservation-2",
                "activity-1",
                "agent-2",
                ReservationStatus.Accepted);
            var migration = new ActivityReservationIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                migration.UpdateFrom1Async);

            Assert.Contains("multiple active reservations", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task QueueItemMigration_ValidLegacyRows_BackfillsUniqueClaims()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-migration-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyQueueItemIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<QueueItemIndex>(store);
            await InsertQueueItemIndexAsync(
                schemaBuilder,
                tableName,
                1,
                "queue-item-1",
                "activity-1",
                QueueItemStatus.Waiting);
            await InsertQueueItemIndexAsync(
                schemaBuilder,
                tableName,
                2,
                "queue-item-2",
                "activity-1",
                QueueItemStatus.Completed);
            var migration = new QueueItemIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal(
                "activity-1",
                await ReadClaimAsync(schemaBuilder, tableName, "ActivityClaimKey", "queue-item-1"));
            Assert.Equal(
                "queue-item-2",
                await ReadClaimAsync(schemaBuilder, tableName, "ActivityClaimKey", "queue-item-2"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task QueueItemMigration_DuplicateLegacyActivityClaims_FailsWithRepairGuidance()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-queue-duplicate-{Guid.NewGuid():N}.db");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyQueueItemIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<QueueItemIndex>(store);
            await InsertQueueItemIndexAsync(
                schemaBuilder,
                tableName,
                1,
                "queue-item-1",
                "activity-1",
                QueueItemStatus.Waiting);
            await InsertQueueItemIndexAsync(
                schemaBuilder,
                tableName,
                2,
                "queue-item-2",
                "activity-1",
                QueueItemStatus.Reserved);
            var migration = new QueueItemIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                migration.UpdateFrom1Async);

            Assert.Contains("multiple active items", exception.Message, StringComparison.Ordinal);
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
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }

    private static Task CreateLegacyReservationIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
    }

    private static Task CreateLegacyQueueItemIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
    }

    private static async Task InsertReservationIndexAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string activityId,
        string agentId,
        ReservationStatus status)
    {
        // YesSql persists the enum as its underlying integer, so a real legacy varchar row holds the integer as
        // text ("0", "1", ...). Seed that representation so the migration's numeric-string comparisons are exercised.
        await ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, ActivityItemId, AgentId, Status, ExpiresUtc)
            VALUES (@DocumentId, @ItemId, @ActivityItemId, @AgentId, @Status, @ExpiresUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@ActivityItemId", activityId),
            ("@AgentId", agentId),
            ("@Status", ((int)status).ToString(CultureInfo.InvariantCulture)),
            ("@ExpiresUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));
    }

    private static async Task InsertQueueItemIndexAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string activityId,
        QueueItemStatus status)
    {
        // YesSql persists the enum as its underlying integer, so a real legacy varchar row holds the integer as
        // text ("0", "1", ...). Seed that representation so the migration's numeric-string comparisons are exercised.
        await ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, QueueId, ActivityItemId, Status, Priority, EnqueuedUtc)
            VALUES (@DocumentId, @ItemId, @QueueId, @ActivityItemId, @Status, @Priority, @EnqueuedUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@QueueId", "queue-1"),
            ("@ActivityItemId", activityId),
            ("@Status", ((int)status).ToString(CultureInfo.InvariantCulture)),
            ("@Priority", ((int)InteractionPriority.Normal).ToString(CultureInfo.InvariantCulture)),
            ("@EnqueuedUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));
    }

    private static async Task<string> ReadClaimAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        string claimColumn,
        string itemId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT {claimColumn} FROM {tableName} WHERE ItemId = @ItemId";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@ItemId";
        parameter.Value = itemId;
        command.Parameters.Add(parameter);

        return (string)await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(
        SchemaBuilder schemaBuilder,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static string GetIndexTableName<TIndex>(IStore store)
    {
        var tableName = store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(
                typeof(TIndex),
                ContactCenterConstants.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(
            tableName,
            store.Configuration.Schema);
    }
}
