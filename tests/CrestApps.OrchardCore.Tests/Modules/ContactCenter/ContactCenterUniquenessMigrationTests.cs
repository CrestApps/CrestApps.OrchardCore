using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Models;
using System.Data.Common;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterUniquenessMigrationTests
{
    [Fact]
    public async Task CallSessionMigration_ValidLegacyRows_BackfillsProviderCallClaims()
    {
        var databasePath = DatabasePath("callsession-backfill");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyCallSessionIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<CallSessionIndex>(store);
            await InsertCallSessionAsync(schemaBuilder, tableName, 1, "session-1", "Asterisk", "call-1");
            await InsertCallSessionAsync(schemaBuilder, tableName, 2, "session-2", null, null);
            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal("Asterisk|call-1", await ReadColumnAsync(schemaBuilder, tableName, "ProviderCallClaimKey", "session-1"));
            Assert.Equal("session-2", await ReadColumnAsync(schemaBuilder, tableName, "ProviderCallClaimKey", "session-2"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CallSessionMigration_DuplicateProviderCall_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("callsession-duplicate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyCallSessionIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<CallSessionIndex>(store);
            await InsertCallSessionAsync(schemaBuilder, tableName, 1, "session-1", "Asterisk", "call-1");
            await InsertCallSessionAsync(schemaBuilder, tableName, 2, "session-2", "Asterisk", "call-1");
            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple call sessions for one provider-call identity", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CallSessionMigration_UniqueIndex_RejectsDuplicateProviderCall()
    {
        var databasePath = DatabasePath("callsession-constraint");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyCallSessionIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<CallSessionIndex>(store);
            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };
            await migration.UpdateFrom1Async();

            await InsertCallSessionAsync(schemaBuilder, tableName, 1, "session-1", "Asterisk", "call-1", "Asterisk|call-1");

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertCallSessionAsync(schemaBuilder, tableName, 2, "session-2", "Asterisk", "call-1", "Asterisk|call-1"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CallSessionMigration_AliasRow_RewritesProviderNameAndClaimToCanonical()
    {
        var databasePath = DatabasePath("callsession-alias-rewrite");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyCallSessionIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<CallSessionIndex>(store);
            await InsertCallSessionAsync(schemaBuilder, tableName, 1, "session-1", "Default Asterisk", "call-1");
            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal("Asterisk", await ReadColumnAsync(schemaBuilder, tableName, "ProviderName", "session-1"));
            Assert.Equal("Asterisk|call-1", await ReadColumnAsync(schemaBuilder, tableName, "ProviderCallClaimKey", "session-1"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task CallSessionMigration_AliasCollidesWithCanonical_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("callsession-alias-collision");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyCallSessionIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<CallSessionIndex>(store);
            await InsertCallSessionAsync(schemaBuilder, tableName, 1, "session-1", "Default Asterisk", "call-1");
            await InsertCallSessionAsync(schemaBuilder, tableName, 2, "session-2", "Asterisk", "call-1");
            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple call sessions for one provider-call identity", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InteractionEventMigration_ValidLegacyRows_BackfillsIdempotencyClaims()
    {
        var databasePath = DatabasePath("event-backfill");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInteractionEventIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<InteractionEventIndex>(store);
            await InsertInteractionEventAsync(schemaBuilder, tableName, 1, "event-1", "keyed");
            await InsertInteractionEventAsync(schemaBuilder, tableName, 2, "event-2", null);
            var migration = new InteractionEventIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal("keyed", await ReadColumnAsync(schemaBuilder, tableName, "IdempotencyClaimKey", "event-1"));
            Assert.Equal("event-2", await ReadColumnAsync(schemaBuilder, tableName, "IdempotencyClaimKey", "event-2"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InteractionEventMigration_DuplicateIdempotencyKey_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("event-duplicate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInteractionEventIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<InteractionEventIndex>(store);
            await InsertInteractionEventAsync(schemaBuilder, tableName, 1, "event-1", "duplicate-key");
            await InsertInteractionEventAsync(schemaBuilder, tableName, 2, "event-2", "duplicate-key");
            var migration = new InteractionEventIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple events with the same idempotency key", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task InteractionEventMigration_UniqueIndex_RejectsDuplicateIdempotencyKey()
    {
        var databasePath = DatabasePath("event-constraint");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInteractionEventIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<InteractionEventIndex>(store);
            var migration = new InteractionEventIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };
            await migration.UpdateFrom1Async();

            await InsertInteractionEventAsync(schemaBuilder, tableName, 1, "event-1", "same-key", "same-key");

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertInteractionEventAsync(schemaBuilder, tableName, 2, "event-2", "same-key", "same-key"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProviderWebhookInboxMigration_DuplicateDelivery_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("inbox-duplicate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInboxIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ProviderWebhookInboxMessageIndex>(store);
            await InsertInboxAsync(schemaBuilder, tableName, 1, "message-1", "Asterisk", "delivery-1");
            await InsertInboxAsync(schemaBuilder, tableName, 2, "message-2", "Asterisk", "delivery-1");
            var migration = new ProviderWebhookInboxMessageIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple messages for one provider delivery", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProviderWebhookInboxMigration_UniqueIndex_RejectsDuplicateDelivery()
    {
        var databasePath = DatabasePath("inbox-constraint");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInboxIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ProviderWebhookInboxMessageIndex>(store);
            var migration = new ProviderWebhookInboxMessageIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };
            await migration.UpdateFrom1Async();

            await InsertInboxAsync(schemaBuilder, tableName, 1, "message-1", "Asterisk", "delivery-1");

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertInboxAsync(schemaBuilder, tableName, 2, "message-2", "Asterisk", "delivery-1"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProviderWebhookInboxMigration_AliasRow_RewritesProviderNameToCanonical()
    {
        var databasePath = DatabasePath("inbox-alias-rewrite");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInboxIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ProviderWebhookInboxMessageIndex>(store);
            await InsertInboxAsync(schemaBuilder, tableName, 1, "message-1", "Default Asterisk", "delivery-1");
            var migration = new ProviderWebhookInboxMessageIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);
            Assert.Equal("Asterisk", await ReadColumnAsync(schemaBuilder, tableName, "ProviderName", "message-1"));

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertInboxAsync(schemaBuilder, tableName, 2, "message-2", "Asterisk", "delivery-1"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProviderWebhookInboxMigration_AliasCollidesWithCanonical_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("inbox-alias-collision");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyInboxIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ProviderWebhookInboxMessageIndex>(store);
            await InsertInboxAsync(schemaBuilder, tableName, 1, "message-1", "Default Asterisk", "delivery-1");
            await InsertInboxAsync(schemaBuilder, tableName, 2, "message-2", "Asterisk", "delivery-1");
            var migration = new ProviderWebhookInboxMessageIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple messages for one provider delivery", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task EventMetricMigration_DuplicateDateAndEvent_FailsWithRepairGuidance()
    {
        var databasePath = DatabasePath("metric-duplicate");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyEventMetricIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ContactCenterEventMetricIndex>(store);
            await InsertEventMetricAsync(schemaBuilder, tableName, 1, "metric-1", "2026-07-15", "OfferAccepted");
            await InsertEventMetricAsync(schemaBuilder, tableName, 2, "metric-2", "2026-07-15", "OfferAccepted");
            var migration = new ContactCenterEventMetricIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(migration.UpdateFrom1Async);

            Assert.Contains("multiple rows for the same date and event type", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task EventMetricMigration_UniqueIndexRejectsDuplicateDateAndEvent()
    {
        var databasePath = DatabasePath("metric-constraint");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyEventMetricIndexAsync(schemaBuilder);
            var tableName = GetIndexTableName<ContactCenterEventMetricIndex>(store);
            var migration = new ContactCenterEventMetricIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };
            await migration.UpdateFrom1Async();
            await InsertEventMetricAsync(schemaBuilder, tableName, 1, "metric-1", "2026-07-15", "OfferAccepted");

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertEventMetricAsync(schemaBuilder, tableName, 2, "metric-2", "2026-07-15", "OfferAccepted"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task ProcessedEventMigration_UniqueIndexRejectsDuplicateHandlerEvent()
    {
        var databasePath = DatabasePath("processed-event-constraint");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            var migration = new ContactCenterProcessedEventIndexMigrations(store)
            {
                SchemaBuilder = schemaBuilder,
            };
            await migration.CreateAsync();
            var tableName = GetIndexTableName<ContactCenterProcessedEventIndex>(store);
            await InsertProcessedEventAsync(schemaBuilder, tableName, 1, "processed-1", "handler/v1", "event-1");

            await Assert.ThrowsAnyAsync<DbException>(() =>
                InsertProcessedEventAsync(schemaBuilder, tableName, 2, "processed-2", "handler/v1", "event-1"));
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static ProviderIdentityResolver CreateAsteriskResolver()
        => new([new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }

    private static Task CreateLegacyCallSessionIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("State", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterConstants.CollectionName);
    }

    private static Task CreateLegacyInteractionEventIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
    }

    private static Task CreateLegacyInboxIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
    }

    private static Task CreateLegacyEventMetricIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.WithLength(128)),
            collection: ContactCenterConstants.CollectionName);
    }

    private static async Task InsertCallSessionAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string providerName,
        string providerCallId,
        string claimKey = null)
    {
        if (claimKey is null)
        {
            await ExecuteAsync(
                schemaBuilder,
                $"""
                INSERT INTO {tableName} (DocumentId, ItemId, ProviderName, ProviderCallId, State, CreatedUtc)
                VALUES (@DocumentId, @ItemId, @ProviderName, @ProviderCallId, @State, @CreatedUtc)
                """,
                ("@DocumentId", documentId),
                ("@ItemId", itemId),
                ("@ProviderName", (object)providerName ?? DBNull.Value),
                ("@ProviderCallId", (object)providerCallId ?? DBNull.Value),
                ("@State", ContactCenterCallState.Connected.ToString()),
                ("@CreatedUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));

            return;
        }

        await ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, ProviderName, ProviderCallId, ProviderCallClaimKey, State, CreatedUtc)
            VALUES (@DocumentId, @ItemId, @ProviderName, @ProviderCallId, @ProviderCallClaimKey, @State, @CreatedUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@ProviderName", (object)providerName ?? DBNull.Value),
            ("@ProviderCallId", (object)providerCallId ?? DBNull.Value),
            ("@ProviderCallClaimKey", claimKey),
            ("@State", ContactCenterCallState.Connected.ToString()),
            ("@CreatedUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));
    }

    private static async Task InsertInteractionEventAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string idempotencyKey,
        string claimKey = null)
    {
        if (claimKey is null)
        {
            await ExecuteAsync(
                schemaBuilder,
                $"""
                INSERT INTO {tableName} (DocumentId, ItemId, EventType, IdempotencyKey, OccurredUtc)
                VALUES (@DocumentId, @ItemId, @EventType, @IdempotencyKey, @OccurredUtc)
                """,
                ("@DocumentId", documentId),
                ("@ItemId", itemId),
                ("@EventType", "Test"),
                ("@IdempotencyKey", (object)idempotencyKey ?? DBNull.Value),
                ("@OccurredUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));

            return;
        }

        await ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, EventType, IdempotencyKey, IdempotencyClaimKey, OccurredUtc)
            VALUES (@DocumentId, @ItemId, @EventType, @IdempotencyKey, @IdempotencyClaimKey, @OccurredUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@EventType", "Test"),
            ("@IdempotencyKey", (object)idempotencyKey ?? DBNull.Value),
            ("@IdempotencyClaimKey", claimKey),
            ("@OccurredUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));
    }

    private static Task InsertInboxAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string providerName,
        string deliveryId)
    {
        return ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, ProviderName, DeliveryId, Status, NextAttemptUtc)
            VALUES (@DocumentId, @ItemId, @ProviderName, @DeliveryId, @Status, @NextAttemptUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@ProviderName", providerName),
            ("@DeliveryId", deliveryId),
            ("@Status", ProviderWebhookInboxStatus.Pending.ToString()),
            ("@NextAttemptUtc", new DateTime(2026, 7, 14, 8, 0, 0, DateTimeKind.Utc)));
    }

    private static Task InsertEventMetricAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string dateKey,
        string eventType)
    {
        return ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, DateKey, Date, EventType)
            VALUES (@DocumentId, @ItemId, @DateKey, @Date, @EventType)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@DateKey", dateKey),
            ("@Date", new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc)),
            ("@EventType", eventType));
    }

    private static Task InsertProcessedEventAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string handlerId,
        string eventId)
    {
        return ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} (DocumentId, ItemId, HandlerId, EventId)
            VALUES (@DocumentId, @ItemId, @HandlerId, @EventId)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@HandlerId", handlerId),
            ("@EventId", eventId));
    }

    private static async Task<string> ReadColumnAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        string columnName,
        string itemId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT {columnName} FROM {tableName} WHERE ItemId = @ItemId";
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
            store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), ContactCenterConstants.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }
}
