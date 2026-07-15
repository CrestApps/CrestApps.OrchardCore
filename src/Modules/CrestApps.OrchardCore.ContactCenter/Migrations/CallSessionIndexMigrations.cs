using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
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
            .Column<string>("State", column => column.WithLength(50))
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

        return 2;
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

    private async Task CanonicalizeAndBackfillClaimKeysAsync(string quotedTableName)
    {
        var documentIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("DocumentId");
        var itemIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("ItemId");
        var providerNameColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderName");
        var providerCallColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderCallId");
        var claimColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderCallClaimKey");

        var rows = new List<(long DocumentId, string CanonicalProviderName, string ProviderCallId, string ClaimKey)>();

        await using (var selectCommand = SchemaBuilder.Connection.CreateCommand())
        {
            selectCommand.Transaction = SchemaBuilder.Transaction;
            selectCommand.CommandText =
                $"SELECT {documentIdColumn}, {itemIdColumn}, {providerNameColumn}, {providerCallColumn} FROM {quotedTableName}";

            await using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var documentId = reader.GetInt64(0);
                var itemId = reader.IsDBNull(1) ? null : reader.GetString(1);
                var providerName = reader.IsDBNull(2) ? null : reader.GetString(2);
                var providerCallId = reader.IsDBNull(3) ? null : reader.GetString(3);

                // Canonicalize the legacy provider alias before building the claim key so that alias-stored
                // and canonical rows for the same provider call collapse to one identity and are detected as
                // duplicates rather than silently persisting distinct alias index values.
                var canonicalProviderName = _providerIdentityResolver.Canonicalize(providerName);
                var claimKey = ContactCenterClaimKeys.BuildProviderCallClaim(canonicalProviderName, providerCallId, itemId);

                rows.Add((documentId, canonicalProviderName, providerCallId, claimKey));
            }
        }

        DetectClaimCollisions(rows);

        foreach (var row in rows)
        {
            await using var updateCommand = SchemaBuilder.Connection.CreateCommand();
            updateCommand.Transaction = SchemaBuilder.Transaction;
            updateCommand.CommandText =
                $"UPDATE {quotedTableName} SET {claimColumn} = @ClaimKey, {providerNameColumn} = @ProviderName WHERE {documentIdColumn} = @DocumentId";
            AddParameter(updateCommand, "@ClaimKey", row.ClaimKey);
            AddParameter(updateCommand, "@ProviderName", (object)row.CanonicalProviderName ?? DBNull.Value);
            AddParameter(updateCommand, "@DocumentId", row.DocumentId);
            await updateCommand.ExecuteNonQueryAsync();
        }
    }

    private static void DetectClaimCollisions(
        List<(long DocumentId, string CanonicalProviderName, string ProviderCallId, string ClaimKey)> rows)
    {
        // Only rows that carry a provider call identifier participate in the provider-call claim; rows without
        // one fall back to their globally unique item identifier and cannot collide.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.ProviderCallId))
            {
                continue;
            }

            if (!seen.Add(row.ClaimKey))
            {
                throw new InvalidOperationException(
                    "The Contact Center call session index contains multiple call sessions for one provider-call identity. Resolve the duplicate legacy call sessions before enabling the provider-call uniqueness constraint.");
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
