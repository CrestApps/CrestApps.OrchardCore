using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Holds the upgrade backfills to a cost that does not grow with the size of the tenant they run against.
/// </summary>
/// <remarks>
/// These backfills run inside the transaction that gates tenant startup, so their cost is startup time. A
/// backfill written as "read the table, compute in memory, update each row" is invisible on a developer
/// database with a handful of rows and fatal on a tenant with a million: the tenant issues a million round
/// trips and never activates. Wall-clock is the wrong instrument for that — it is dominated by the machine
/// and would either flap or be set so loose it proves nothing. What actually distinguishes the two shapes is
/// whether the number of statements tracks the number of rows, so that is what is measured: the same
/// migration is run against a small tenant and a ten-times-larger one and the statement counts must match.
/// </remarks>
public sealed class MigrationStartupBudgetTests
{
    [Fact]
    public async Task CallSessionClaimBackfill_WhenTheTenantIsTenTimesLarger_DoesNotIssueMoreStatements()
    {
        var small = await MeasureCallSessionBackfillAsync(rowCount: 20);
        var large = await MeasureCallSessionBackfillAsync(rowCount: 200);

        Assert.Equal(small, large);
    }

    [Fact]
    public async Task InboxCanonicalizationBackfill_WhenTheTenantIsTenTimesLarger_DoesNotIssueMoreStatements()
    {
        var small = await MeasureInboxCanonicalizationAsync(rowCount: 20);
        var large = await MeasureInboxCanonicalizationAsync(rowCount: 200);

        Assert.Equal(small, large);
    }

    private static async Task<int> MeasureCallSessionBackfillAsync(int rowCount)
    {
        var databasePath = DatabasePath("call-session-budget");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = await CreateStoreAsync(databasePath, connectionFactory);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            await CreateLegacyCallSessionIndexAsync(schemaBuilder);

            var tableName = GetIndexTableName<CallSessionIndex>(store);

            for (var index = 0; index < rowCount; index++)
            {
                // Half the rows carry a legacy alias so the canonicalization path is measured, not skipped.
                await InsertCallSessionAsync(
                    schemaBuilder,
                    tableName,
                    index + 1,
                    $"session-{index}",
                    index % 2 == 0 ? "Default Asterisk" : "Asterisk",
                    $"provider-call-{index}");
            }

            var migration = new CallSessionIndexMigrations(store, CreateAsteriskResolver())
            {
                SchemaBuilder = schemaBuilder,
            };

            connectionFactory.Clear();

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);

            return connectionFactory.Statements.Count;
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<int> MeasureInboxCanonicalizationAsync(int rowCount)
    {
        var databasePath = DatabasePath("inbox-budget");
        var connectionFactory = new RecordingConnectionFactory($"Data Source={databasePath};Pooling=False");
        var store = await CreateStoreAsync(databasePath, connectionFactory);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            await CreateLegacyInboxIndexAsync(schemaBuilder);

            var tableName = GetIndexTableName<ProviderWebhookInboxMessageIndex>(store);

            for (var index = 0; index < rowCount; index++)
            {
                await InsertInboxMessageAsync(
                    schemaBuilder,
                    tableName,
                    index + 1,
                    $"inbox-{index}",
                    index % 2 == 0 ? "Default Asterisk" : "Asterisk",
                    $"delivery-{index}");
            }

            var migration = new ProviderWebhookInboxMessageIndexMigrations(store, CreateAsteriskResolver(), new StubClock())
            {
                SchemaBuilder = schemaBuilder,
            };

            connectionFactory.Clear();

            var version = await migration.UpdateFrom1Async();

            Assert.Equal(2, version);

            return connectionFactory.Statements.Count;
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-{prefix}-{Guid.NewGuid():N}.db");

    private static ProviderIdentityResolver CreateAsteriskResolver()
        => new([new TestProviderIdentityProvider(new ProviderIdentity("Asterisk", "Default Asterisk"))]);

    private static async Task<IStore> CreateStoreAsync(string databasePath, IConnectionFactory connectionFactory)
    {
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ConnectionFactory = connectionFactory;
        });

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, TestContext.Current.CancellationToken);

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
            .Column<VoiceCallState>("State")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName);
    }

    private static Task CreateLegacyInboxIndexAsync(SchemaBuilder schemaBuilder)
    {
        return schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("CreatedUtc"),
            collection: ContactCenterStorage.CollectionName);
    }

    private static Task InsertCallSessionAsync(
        SchemaBuilder schemaBuilder,
        string tableName,
        long documentId,
        string itemId,
        string providerName,
        string providerCallId)
    {
        return ExecuteAsync(
            schemaBuilder,
            $"""
            INSERT INTO {tableName} ("DocumentId", "ItemId", "ProviderName", "ProviderCallId", "State", "CreatedUtc")
            VALUES (@DocumentId, @ItemId, @ProviderName, @ProviderCallId, 0, @CreatedUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@ProviderName", providerName),
            ("@ProviderCallId", providerCallId),
            ("@CreatedUtc", DateTime.UnixEpoch));
    }

    private static Task InsertInboxMessageAsync(
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
            INSERT INTO {tableName} ("DocumentId", "ItemId", "ProviderName", "DeliveryId", "Status", "NextAttemptUtc", "CreatedUtc")
            VALUES (@DocumentId, @ItemId, @ProviderName, @DeliveryId, 0, @NextAttemptUtc, @CreatedUtc)
            """,
            ("@DocumentId", documentId),
            ("@ItemId", itemId),
            ("@ProviderName", providerName),
            ("@DeliveryId", deliveryId),
            ("@NextAttemptUtc", DateTime.UnixEpoch),
            ("@CreatedUtc", DateTime.UnixEpoch));
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
            store.Configuration.TableNameConvention.GetIndexTable(typeof(TIndex), ContactCenterStorage.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }
}
