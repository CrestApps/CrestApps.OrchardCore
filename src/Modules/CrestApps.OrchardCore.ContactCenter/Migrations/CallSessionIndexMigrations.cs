using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
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
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("ProviderCallClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(261))
            .Column<VoiceCallState>("State")
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "ProviderCallId",
                "InteractionId",
                "State"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallSessionIndex>(table => table
            .CreateIndex("IDX_CallSessionIndex_Lookup",
                "ActivityItemId",
                "AgentId",
                "QueueId"),
            collection: ContactCenterConstants.CollectionName
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
            collection: ContactCenterConstants.CollectionName);

        return 3;
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
            collection: ContactCenterConstants.CollectionName);

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
            collection: ContactCenterConstants.CollectionName);

        return 3;
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
