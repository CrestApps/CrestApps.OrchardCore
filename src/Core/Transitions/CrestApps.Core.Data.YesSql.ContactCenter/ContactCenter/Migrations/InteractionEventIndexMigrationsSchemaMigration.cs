using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using YesSql;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionEventIndex"/> and enforces database-backed
/// idempotency-key uniqueness.
/// </summary>
public sealed class InteractionEventIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public InteractionEventIndexMigrationsSchemaMigration(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "InteractionEventIndexMigrations";

    /// <summary>
    /// Creates the interaction event index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<string>("IdempotencyClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Interaction",
                "InteractionId",
                "OccurredUtc",
                "EventType"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Idempotency",
                "IdempotencyKey"),
            collection: ContactCenterStorage.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(InteractionEventIndex),
            "UQ_InteractionEventIndex_IdempotencyClaimKey",
            "IdempotencyClaimKey");

        return 2;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            case 2:
                return await UpdateFrom2Async(builder);

            default:
                return version;
        }
    }

    private static async Task EnsureLegacyRowsCanBeConstrainedAsync(ISchemaBuilder builder, string quotedTableName, string idempotencyColumn)
    {
        var hasDuplicateKeys = await ContactCenterMigrationSql.ExistsAsync(
            builder,
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

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the portable idempotency claim column and unique constraint to existing interaction event indexes.
                    var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(builder, _store, typeof(InteractionEventIndex));
                    var claimColumn = builder.Dialect.QuoteForColumnName("IdempotencyClaimKey");
                    var idempotencyColumn = builder.Dialect.QuoteForColumnName("IdempotencyKey");
                    var itemIdColumn = builder.Dialect.QuoteForColumnName("ItemId");

                    await EnsureLegacyRowsCanBeConstrainedAsync(builder, quotedTableName, idempotencyColumn);

                    await builder.AlterIndexTableAsync<InteractionEventIndex>(table => table
                        .AddColumn<string>(
                            "IdempotencyClaimKey",
                            column => column.NotNull().WithDefault(string.Empty).WithLength(128)),
                        collection: ContactCenterStorage.CollectionName);

                    await using (var command = builder.Connection.CreateCommand())
                    {
                        command.Transaction = builder.Transaction;
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
                        builder,
                        _store,
                        typeof(InteractionEventIndex),
                        "UQ_InteractionEventIndex_IdempotencyClaimKey",
                        "IdempotencyClaimKey");

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private static async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
                    // is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
                    await builder.AlterIndexTableAsync<InteractionEventIndex>(table => table
                        .CreateIndex(
                            "IDX_InteractionEventIndex_Retention",
                            "OccurredUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }
}
