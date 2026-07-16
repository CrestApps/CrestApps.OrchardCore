using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionEventIndex"/> and enforces database-backed
/// idempotency-key uniqueness.
/// </summary>
internal sealed class InteractionEventIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public InteractionEventIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the interaction event index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<string>("IdempotencyClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Interaction",
                "InteractionId",
                "OccurredUtc",
                "EventType"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Idempotency",
                "IdempotencyKey"),
            collection: ContactCenterConstants.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(InteractionEventIndex),
            "UQ_InteractionEventIndex_IdempotencyClaimKey",
            "IdempotencyClaimKey");

        return 2;
    }

    /// <summary>
    /// Adds the portable idempotency claim column and unique constraint to existing interaction event indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(InteractionEventIndex));
        var claimColumn = SchemaBuilder.Dialect.QuoteForColumnName("IdempotencyClaimKey");
        var idempotencyColumn = SchemaBuilder.Dialect.QuoteForColumnName("IdempotencyKey");
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");

        await EnsureLegacyRowsCanBeConstrainedAsync(quotedTableName, idempotencyColumn);

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .AddColumn<string>(
                "IdempotencyClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(128)),
            collection: ContactCenterConstants.CollectionName);

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = $"""
                UPDATE {quotedTableName}
                SET {claimColumn} = CASE
                        WHEN {idempotencyColumn} IS NULL OR {idempotencyColumn} = '' THEN {itemIdColumn}
                        ELSE {idempotencyColumn}
                    END
                """;
            await command.ExecuteNonQueryAsync();
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(InteractionEventIndex),
            "UQ_InteractionEventIndex_IdempotencyClaimKey",
            "IdempotencyClaimKey");

        return 2;
    }

    private async Task EnsureLegacyRowsCanBeConstrainedAsync(string quotedTableName, string idempotencyColumn)
    {
        var hasDuplicateKeys = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {idempotencyColumn} IS NOT NULL AND {idempotencyColumn} <> ''
            GROUP BY {idempotencyColumn}
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateKeys)
        {
            throw new InvalidOperationException(
                "The Contact Center interaction event index contains multiple events with the same idempotency key. Resolve the duplicate legacy events before enabling the idempotency uniqueness constraint.");
        }
    }
}
