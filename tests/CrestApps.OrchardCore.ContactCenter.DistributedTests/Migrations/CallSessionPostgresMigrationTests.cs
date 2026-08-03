using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Npgsql;
using YesSql;
using YesSql.Provider.PostgreSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.Migrations;

/// <summary>
/// Runs the call session provider-call widening upgrade against PostgreSQL, the engine single-node production
/// deployments run.
/// </summary>
/// <remarks>
/// The SQLite rolling-upgrade harness proves the rebuild's mechanics and that it preserves the values already
/// stored, but SQLite stores every text column as unbounded <c>TEXT</c>, so it can neither reproduce the narrow
/// column the rebuild has to widen nor prove that the widening took. That is the whole point of the work — a
/// provider call identifier that the old column truncated on an enforcing engine is stored and matched in full
/// after the upgrade — so the upgrade is additionally executed here against a real enforcing engine. The
/// historical table is built at the literal pre-widen lengths, seeded with a value the narrow column could hold,
/// upgraded, and then checked to have grown to the wider lengths while keeping the seeded value and admitting a
/// value the narrow column never could.
/// </remarks>
public sealed class CallSessionPostgresMigrationTests
{
    private const string TablePrefix = "tp_";

    // The lengths the column carried before the widening, taken from the historical migration that created them.
    private const int HistoricalProviderNameLength = 128;
    private const int HistoricalProviderCallIdLength = 128;
    private const int HistoricalProviderCallClaimKeyLength = 261;

    // The lengths the widening must produce.
    private const int WidenedProviderCallIdLength = 256;
    private const int WidenedProviderCallClaimKeyLength = 385;

    [Fact]
    public async Task CallSessionUpgrade_FromTheNarrowSchema_WidensColumnsPreservesValuesAndActivates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = RequireConnectionString();
        var schema = $"mig{Guid.NewGuid():N}";

        await ExecuteAsync(connectionString, $"CREATE SCHEMA \"{schema}\";", cancellationToken);

        var store = CreateStore(connectionString, schema);

        try
        {
            await store.InitializeAsync(cancellationToken);
            await store.InitializeCollectionAsync(ContactCenterStorage.CollectionName, cancellationToken);

            await using (var session = store.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(cancellationToken);
                var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

                await CreateHistoricalTableAsync(schemaBuilder, store);
                await SeedAsync(schemaBuilder, store);

                var migration = new CallSessionIndexMigrations(store, new PassThroughProviderIdentityResolver())
                {
                    SchemaBuilder = schemaBuilder,
                };

                var version = await migration.UpdateFrom3Async();

                Assert.Equal(4, version);

                await transaction.CommitAsync(cancellationToken);
            }

            var providerCallIdLength = await ReadColumnLengthAsync(connectionString, schema, store, "ProviderCallId", cancellationToken);
            var claimKeyLength = await ReadColumnLengthAsync(connectionString, schema, store, "ProviderCallClaimKey", cancellationToken);

            Assert.Equal(WidenedProviderCallIdLength, providerCallIdLength);
            Assert.Equal(WidenedProviderCallClaimKeyLength, claimKeyLength);

            var preservedCallId = await ReadProviderCallIdAsync(connectionString, store, 1, cancellationToken);

            Assert.Equal("call-1", preservedCallId);

            // A duplicate claim key must be rejected, which proves the unique index survived the rebuild and
            // still enforces one call session per canonical provider-call identity over the widened column. The
            // failure is pinned to the unique violation (SQLSTATE 23505) on the claim-key index so the assertion
            // cannot pass for an unrelated reason such as a document-table conflict.
            var uniqueViolation = await Assert.ThrowsAnyAsync<PostgresException>(() => InsertCallSessionAsync(
                connectionString,
                store,
                documentId: 3,
                itemId: "item-3",
                providerCallId: "call-1",
                providerCallClaimKey: "Asterisk|call-1",
                cancellationToken));

            Assert.Equal(PostgresErrorCodes.UniqueViolation, uniqueViolation.SqlState);
            Assert.Contains("ProviderCallClaimKey", uniqueViolation.ConstraintName);

            // A provider call identifier at the full widened length, and the claim key composed from it, both
            // exceed what the narrow schema could hold, so a row that stores them in full proves the widening
            // took on an engine that enforces the declared length — for the claim key too, not the identifier
            // alone. The claim key here is "Asterisk|" (9) + 256 = 265, past the old 261 ceiling.
            var longProviderCallId = new string('a', WidenedProviderCallIdLength);
            var longClaimKey = "Asterisk|" + longProviderCallId;

            await InsertCallSessionAsync(
                connectionString,
                store,
                documentId: 4,
                itemId: "item-4",
                providerCallId: longProviderCallId,
                providerCallClaimKey: longClaimKey,
                cancellationToken);

            var storedLongCallId = await ReadProviderCallIdAsync(connectionString, store, 4, cancellationToken);

            Assert.Equal(longProviderCallId, storedLongCallId);
        }
        finally
        {
            store.Dispose();
            await ExecuteAsync(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", cancellationToken);
        }
    }

    private static async Task CreateHistoricalTableAsync(SchemaBuilder schemaBuilder, IStore store)
    {
        await schemaBuilder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(HistoricalProviderNameLength))
            .Column<string>("ProviderCallId", column => column.WithLength(HistoricalProviderCallIdLength))
            .Column<string>("ProviderCallClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(HistoricalProviderCallClaimKeyLength))
            .Column<VoiceCallState>("State")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProviderCallId",
                "InteractionId",
                "State"),
            collection: ContactCenterStorage.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_Lookup",
                "ActivityItemId",
                "AgentId",
                "QueueId"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            schemaBuilder,
            store,
            typeof(CallSessionIndex),
            "UQ_CallSessionIndex_ProviderCallClaimKey",
            "ProviderCallClaimKey");

        await schemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_Retention",
                "EndedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);
    }

    private static async Task SeedAsync(SchemaBuilder schemaBuilder, IStore store)
    {
        var quotedDocumentTableName = QuotedDocumentTableName(store);

        await InsertDocumentAsync(schemaBuilder, quotedDocumentTableName, 1);
        await InsertDocumentAsync(schemaBuilder, quotedDocumentTableName, 2);

        var quotedTableName = QuotedTableName(store);

        await InsertCallSessionAsync(schemaBuilder, quotedTableName, 1, "item-1", "call-1", "Asterisk|call-1");
        await InsertCallSessionAsync(schemaBuilder, quotedTableName, 2, "item-2", "call-2", "Asterisk|call-2");
    }

    private static async Task InsertDocumentAsync(
        SchemaBuilder schemaBuilder,
        string quotedDocumentTableName,
        long documentId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            $"""
            INSERT INTO {quotedDocumentTableName} ("Id", "Type", "Content", "Version")
            VALUES (@Id, @Type, @Content, 1)
            """;

        AddParameter(command, "@Id", documentId);
        AddParameter(command, "@Type", typeof(CallSessionIndex).FullName);
        AddParameter(command, "@Content", "{}");

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertCallSessionAsync(
        SchemaBuilder schemaBuilder,
        string quotedTableName,
        long documentId,
        string itemId,
        string providerCallId,
        string providerCallClaimKey)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            $"""
            INSERT INTO {quotedTableName} ("DocumentId", "ItemId", "ProviderName", "ProviderCallId", "ProviderCallClaimKey", "State", "CreatedUtc")
            VALUES (@DocumentId, @ItemId, @ProviderName, @ProviderCallId, @ProviderCallClaimKey, @State, @CreatedUtc)
            """;

        AddParameter(command, "@DocumentId", documentId);
        AddParameter(command, "@ItemId", itemId);
        AddParameter(command, "@ProviderName", "Asterisk");
        AddParameter(command, "@ProviderCallId", providerCallId);
        AddParameter(command, "@ProviderCallClaimKey", providerCallClaimKey);
        AddParameter(command, "@State", (int)VoiceCallState.Ringing);
        AddParameter(command, "@CreatedUtc", DateTime.UnixEpoch);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertCallSessionAsync(
        string connectionString,
        IStore store,
        long documentId,
        string itemId,
        string providerCallId,
        string providerCallClaimKey,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var documentCommand = connection.CreateCommand();
        documentCommand.CommandText =
            $"""
            INSERT INTO {QuotedDocumentTableName(store)} ("Id", "Type", "Content", "Version")
            VALUES (@Id, @Type, @Content, 1)
            """;

        AddParameter(documentCommand, "@Id", documentId);
        AddParameter(documentCommand, "@Type", typeof(CallSessionIndex).FullName);
        AddParameter(documentCommand, "@Content", "{}");

        await documentCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO {QuotedTableName(store)} ("DocumentId", "ItemId", "ProviderName", "ProviderCallId", "ProviderCallClaimKey", "State", "CreatedUtc")
            VALUES (@DocumentId, @ItemId, @ProviderName, @ProviderCallId, @ProviderCallClaimKey, @State, @CreatedUtc)
            """;

        AddParameter(command, "@DocumentId", documentId);
        AddParameter(command, "@ItemId", itemId);
        AddParameter(command, "@ProviderName", "Asterisk");
        AddParameter(command, "@ProviderCallId", providerCallId);
        AddParameter(command, "@ProviderCallClaimKey", providerCallClaimKey);
        AddParameter(command, "@State", (int)VoiceCallState.Ringing);
        AddParameter(command, "@CreatedUtc", DateTime.UnixEpoch);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ReadColumnLengthAsync(
        string connectionString,
        string schema,
        IStore store,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table AND column_name = @column
            """;

        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", PhysicalTableName(store));
        AddParameter(command, "@column", columnName);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(result);
    }

    private static async Task<string> ReadProviderCallIdAsync(
        string connectionString,
        IStore store,
        long documentId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"ProviderCallId\" FROM {QuotedTableName(store)} WHERE \"DocumentId\" = @DocumentId";

        AddParameter(command, "@DocumentId", documentId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result as string;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string PhysicalTableName(IStore store)
        => store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(CallSessionIndex), ContactCenterStorage.CollectionName);

    private static string QuotedTableName(IStore store)
        => store.Configuration.SqlDialect.QuoteForTableName(PhysicalTableName(store), store.Configuration.Schema);

    private static string QuotedDocumentTableName(IStore store)
    {
        var tableName = store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetDocumentTable(ContactCenterStorage.CollectionName);

        return store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }

    private static IStore CreateStore(string connectionString, string schema)
        => StoreFactory.Create(configuration =>
        {
            configuration.UsePostgreSql(connectionString);
            configuration.Schema = schema;

            // A named schema and a table prefix are independent tenant options, and it is their combination that
            // exposes an index drop naming an index that was never created, so both are set here.
            configuration.TablePrefix = TablePrefix;
        });

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_CENTER_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CONTACT_CENTER_POSTGRES_CONNECTION must point to the PostgreSQL instance used by the Contact Center migration safety gate.");
        }

        return connectionString;
    }

    private static async Task ExecuteAsync(string connectionString, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed class PassThroughProviderIdentityResolver : IProviderIdentityResolver
    {
        public string Canonicalize(string providerName)
            => providerName;
    }
}
