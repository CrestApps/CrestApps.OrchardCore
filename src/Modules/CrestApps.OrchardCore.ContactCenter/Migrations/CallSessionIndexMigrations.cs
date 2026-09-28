using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallSessionIndex"/> and enforces one call session per canonical
/// provider-call identity.
/// </summary>
internal sealed class CallSessionIndexMigrations : DataMigration
{
    // The provider technical name is a canonical internal slug and stays bounded. The provider call identifier
    // is supplied verbatim by an external switch (a SIP Call-ID can be long), so the original 128 was too short
    // for it. An engine that enforces a declared length rejects an over-length write outright (PostgreSQL 22001,
    // SQL Server 8152, MySQL 1406 under the default strict mode), so the session is never persisted; only
    // non-strict MySQL truncates, and a truncated identifier both breaks lookups by that identifier and — once
    // the claim key composed from it outgrows its own column — forges a collision that defeats the uniqueness
    // the claim exists to enforce. Widening is forward-only: it lets the full identifier be stored going forward
    // and repairs no already-rejected or already-truncated row. 256 is a deliberate ceiling — comfortably longer
    // than any real provider call identifier while keeping the composed claim key within SQL Server's 900-byte
    // unique-index key limit. The claim key length is derived from the two parts it concatenates so it can never
    // truncate a value the source columns can hold, and 385 stays within that limit.
    private const int ProviderNameLength = ContactCenterStorage.ProviderNameLength;
    private const int ProviderCallIdLength = 256;
    private const int ProviderCallClaimKeyLength = ProviderNameLength + 1 + ProviderCallIdLength;

    // The indexes over the columns the widening rebuilds. SQLite refuses to drop a column an index refers to, so
    // each comes down before the rebuild and is recreated after it.
    private static readonly string[] _rebuiltColumnIndexNames =
    [
        "UQ_CallSessionIndex_ProviderCallClaimKey",
        "IDX_CallSessionIndex_DocumentId",
    ];

    private readonly IStore _store;
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public CallSessionIndexMigrations(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver)
    {
        _store = store;
        _providerIdentityResolver = providerIdentityResolver;
    }

    /// <summary>
    /// Creates the call session index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ProviderNameLength))
            .Column<string>("ProviderCallId", column => column.WithLength(ProviderCallIdLength))
            .Column<string>("ProviderCallClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(ProviderCallClaimKeyLength))
            .Column<VoiceCallState>("State")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProviderCallId",
                "InteractionId",
                "State"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_Lookup",
                "ActivityItemId",
                "AgentId",
                "QueueId"),
            collection: ContactCenterStorage.CollectionName
        );

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(CallSessionIndex),
            "UQ_CallSessionIndex_ProviderCallClaimKey",
            "ProviderCallClaimKey");


        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex(
                "IDX_CallSessionIndex_Retention",
                "EndedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 4;
    }

    /// <summary>
    /// Adds the portable provider-call claim column and unique constraint to existing call session indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(CallSessionIndex));

        await EnsureItemIdentifiersPresentAsync(quotedTableName);

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .AddColumn<string>(
                "ProviderCallClaimKey",
                column => column.NotNull().WithDefault(string.Empty).WithLength(261)),
            collection: ContactCenterStorage.CollectionName);

        await CanonicalizeAndBackfillClaimKeysAsync(quotedTableName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(CallSessionIndex),
            "UQ_CallSessionIndex_ProviderCallClaimKey",
            "ProviderCallClaimKey");

        return 2;
    }

    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex(
                "IDX_CallSessionIndex_Retention",
                "EndedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Widens the provider-call identifier and the claim key composed from it so an external switch's long call
    /// identifier is stored and matched in full on the engines that enforce a declared column length.
    /// </summary>
    /// <remarks>
    /// The provider call identifier arrives verbatim from an external switch and can be long, but the column
    /// declared it at a length too short for it. An engine that enforces a declared length rejects an over-length
    /// write outright (PostgreSQL <c>22001</c>, SQL Server <c>8152</c>, MySQL <c>1406</c> under the default strict
    /// mode), so the session is never persisted; only non-strict MySQL truncates, and a truncated identifier
    /// stops a reconciliation lookup from finding its own session and — once the claim key composed from it
    /// outgrows its own column — forges a collision between two distinct calls, the opposite of what the unique
    /// claim exists to guarantee. Widening is forward-only and repairs no existing row. SQLite stores every text
    /// column as unbounded <c>TEXT</c>, so it never rejected or truncated and this rebuild is a value-preserving
    /// no-op there; the widening exists for the engines that enforce the length. The unique claim index and the
    /// covering index that both name a rebuilt column come down before the rebuild — SQLite refuses to drop a
    /// column an index refers to — and are recreated at the wider columns afterwards.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that
        // belongs to a named schema, so it silently drops nothing and the recreation below fails. The qualified
        // drop runs first and is a no-op wherever the data layer's own statement is already sufficient.
        foreach (var indexName in _rebuiltColumnIndexNames)
        {
            var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
                SchemaBuilder,
                _store,
                typeof(CallSessionIndex),
                indexName,
                ContactCenterStorage.CollectionName);

            if (qualifiedIndexName is null)
            {
                continue;
            }

            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "drop index if exists " + qualifiedIndexName;

            await command.ExecuteNonQueryAsync();
        }

        // The drops are tolerant because MySQL commits each schema change on its own and writes this drop
        // without IF EXISTS, so an attempt that stopped part-way would otherwise fail every activation from here
        // on. A drop that genuinely fails is still reported, because the recreation below runs on the strict
        // builder. Each index is dropped in its own alter so a swallowed failure of one — a re-run meeting an
        // index a previous attempt already dropped — cannot suppress the drop of the next: the data layer runs
        // every statement of a single alter under one try, so batching the drops would let the first failure
        // strand the rest, and a surviving index would then make its own recreation below fail every activation.
        var tolerantSchemaBuilder = new SchemaBuilder(
            _store.Configuration,
            SchemaBuilder.Transaction,
            throwOnError: false);

        await tolerantSchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(
            table => table.DropIndex("UQ_CallSessionIndex_ProviderCallClaimKey"),
            collection: ContactCenterStorage.CollectionName);

        await tolerantSchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(
            table => table.DropIndex("IDX_CallSessionIndex_DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await IndexStringColumnRebuild.WidenAsync<CallSessionIndex>(
            SchemaBuilder,
            _store,
            "ProviderCallId",
            ProviderCallIdLength,
            isNotNull: false,
            defaultValue: null,
            ContactCenterStorage.CollectionName);

        await IndexStringColumnRebuild.WidenAsync<CallSessionIndex>(
            SchemaBuilder,
            _store,
            "ProviderCallClaimKey",
            ProviderCallClaimKeyLength,
            isNotNull: true,
            defaultValue: string.Empty,
            ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProviderCallId",
                "InteractionId",
                "State"),
            collection: ContactCenterStorage.CollectionName);

        await EnsureNoDuplicateClaimKeysAsync();

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(CallSessionIndex),
            "UQ_CallSessionIndex_ProviderCallClaimKey",
            "ProviderCallClaimKey");

        return 4;
    }

    private async Task EnsureNoDuplicateClaimKeysAsync()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(CallSessionIndex));
        var claimColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderCallClaimKey");

        // The rebuild copies the claim key verbatim, so a transactional engine recreates the unique index over
        // values that were already unique under it and this check never fires. MySQL commits each schema change
        // on its own, so the index is genuinely absent between the drop and the recreation, and a previous
        // version node could write a colliding key in that window; recreating the index would then fail with an
        // opaque error and the tenant could never activate. This is a non-locking read and DDL still autocommits,
        // so it does not close that window — a write landing between this check and the recreation still fails
        // opaquely — but it narrows the exposure to a single statement and turns the common case into an
        // actionable repair message. Grouping by the composed claim column the index enforces — rather than
        // re-deriving it — is what makes the check a value-for-value mirror of the constraint.
        var hasDuplicateClaims = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            GROUP BY {claimColumn}
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center call session index contains multiple call sessions for one provider-call claim key. Resolve the duplicate legacy call sessions before recreating the provider-call uniqueness constraint.");
        }
    }

    private async Task EnsureItemIdentifiersPresentAsync(string quotedTableName)
    {
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");

        var hasMissingIdentifiers = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {itemIdColumn} IS NULL OR {itemIdColumn} = ''
            """);

        if (hasMissingIdentifiers)
        {
            throw new InvalidOperationException(
                "The Contact Center call session index contains rows without item identifiers. Repair the legacy rows before enabling the provider-call uniqueness constraint.");
        }
    }

    /// <remarks>
    /// Every statement here is whole-table. The obvious implementation reads the table, computes each key in
    /// C#, and issues one UPDATE per row; that runs inside the transaction that gates tenant startup, so a
    /// tenant with a million call sessions needs a million round trips and never finishes activating.
    /// Canonicalization is a lookup over a finite alias map, so it is applied once per distinct alias the
    /// table actually holds rather than once per row, and the claim key is derived by the database from
    /// columns it already has.
    /// </remarks>
    private async Task CanonicalizeAndBackfillClaimKeysAsync(string quotedTableName)
    {
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");
        var providerNameColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderName");
        var providerCallColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderCallId");
        var claimColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderCallClaimKey");

        await CanonicalizeProviderNamesAsync(quotedTableName, providerNameColumn);

        var claimKeyExpression = BuildClaimKeyExpression(providerNameColumn, providerCallColumn);

        await EnsureNoDuplicateClaimsAsync(quotedTableName, providerCallColumn, claimKeyExpression);

        // A session without a provider call identifier claims its own globally unique item identifier, which
        // is what BuildProviderCallClaim returns, so rows that cannot participate still satisfy the not-null
        // unique column.
        await ContactCenterMigrationSql.ExecuteAsync(
            SchemaBuilder,
            $"""
            UPDATE {quotedTableName}
            SET {claimColumn} = {itemIdColumn}
            WHERE {providerCallColumn} IS NULL OR {providerCallColumn} = ''
            """);

        await ContactCenterMigrationSql.ExecuteAsync(
            SchemaBuilder,
            $"""
            UPDATE {quotedTableName}
            SET {claimColumn} = {claimKeyExpression}
            WHERE {providerCallColumn} IS NOT NULL AND {providerCallColumn} <> ''
            """);
    }

    private async Task CanonicalizeProviderNamesAsync(string quotedTableName, string providerNameColumn)
    {
        var aliases = new List<string>();

        await using (var selectCommand = SchemaBuilder.Connection.CreateCommand())
        {
            selectCommand.Transaction = SchemaBuilder.Transaction;
            selectCommand.CommandText =
                $"SELECT DISTINCT {providerNameColumn} FROM {quotedTableName} WHERE {providerNameColumn} IS NOT NULL";

            await using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    aliases.Add(reader.GetString(0));
                }
            }
        }

        foreach (var alias in aliases)
        {
            var canonical = _providerIdentityResolver.Canonicalize(alias);

            if (string.Equals(canonical, alias, StringComparison.Ordinal))
            {
                continue;
            }

            await ContactCenterMigrationSql.ExecuteAsync(
                SchemaBuilder,
                $"UPDATE {quotedTableName} SET {providerNameColumn} = @Canonical WHERE {providerNameColumn} = @Alias",
                ("@Canonical", canonical),
                ("@Alias", alias));
        }
    }

    private string BuildClaimKeyExpression(string providerNameColumn, string providerCallColumn)
    {
        // BuildProviderCallClaim interpolates a null provider name as an empty string, so the database has to
        // do the same or a backfilled row would carry a different key than the index provider later writes.
        return ContactCenterMigrationSql.BuildConcat(
            SchemaBuilder.Dialect,
            $"COALESCE({providerNameColumn}, '')",
            "'|'",
            providerCallColumn);
    }

    private async Task EnsureNoDuplicateClaimsAsync(
        string quotedTableName,
        string providerCallColumn,
        string claimKeyExpression)
    {
        // Grouping by the composed key rather than by the two columns is deliberate: the unique index is over
        // the composed value, so a pair that collides only once composed is still reported. Rows without a
        // provider call identifier are excluded because their key falls back to the row's own identifier, which
        // is unique by construction; a duplicate there would be a corrupt table rather than a legacy claim, and
        // it surfaces as the index creation failing rather than as this repair message.
        var hasDuplicateClaims = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            WHERE {providerCallColumn} IS NOT NULL AND {providerCallColumn} <> ''
            GROUP BY {claimKeyExpression}
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateClaims)
        {
            throw new InvalidOperationException(
                "The Contact Center call session index contains multiple call sessions for one provider-call identity. Resolve the duplicate legacy call sessions before enabling the provider-call uniqueness constraint.");
        }
    }
}
