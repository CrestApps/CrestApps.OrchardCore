using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.Core.Telephony.Services;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the provider webhook inbox index schema and enforces canonical provider-delivery uniqueness.
/// </summary>
internal sealed class ProviderWebhookInboxMessageIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public ProviderWebhookInboxMessageIndexMigrationsSchemaMigration(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
        _providerIdentityResolver = providerIdentityResolver;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ProviderWebhookInboxMessageIndexMigrations";

    /// <summary>
    /// Creates the inbox index table and its lookup and due-message indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ContactCenterStorage.ProviderNameLength))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<ProviderWebhookInboxStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime?>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await builder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table =>
        {
            table.CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Delivery",
                "ProviderName",
                "DeliveryId",
                "DocumentId");
            table.CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Due",
                "Status",
                "NextAttemptUtc",
                "DocumentId");
            table.CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Retention",
                "Status",
                "ProcessedUtc",
                "DocumentId");
        },
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ProviderWebhookInboxMessageIndex),
            "UQ_ProviderWebhookInboxMessageIndex_Delivery",
            "ProviderName",
            "DeliveryId");

        return 3;
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

    /// <remarks>
    /// Canonicalization is a lookup over a finite alias map, so the table is asked which aliases it actually
    /// holds and each one is rewritten with a single statement. Reading every row and issuing an UPDATE per
    /// row would put a round trip per inbox message inside the transaction that gates tenant startup, which a
    /// tenant with a large delivery history never gets through.
    /// </remarks>
    private async Task CanonicalizeProviderNamesAsync(ISchemaBuilder builder, string quotedTableName)
    {
        var providerNameColumn = builder.Dialect.QuoteForColumnName("ProviderName");
        var deliveryIdColumn = builder.Dialect.QuoteForColumnName("DeliveryId");

        var aliases = new List<string>();

        await using (var selectCommand = builder.Connection.CreateCommand())
        {
            selectCommand.Transaction = builder.Transaction;
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

            // Canonicalize the legacy provider alias before duplicate detection so alias-stored and
            // canonical deliveries for one provider collapse to a single identity that the composite
            // (ProviderName, DeliveryId) unique index can enforce.
            await ContactCenterMigrationSql.ExecuteAsync(
                builder,
                $"UPDATE {quotedTableName} SET {providerNameColumn} = @Canonical WHERE {providerNameColumn} = @Alias",
                ("@Canonical", canonical),
                ("@Alias", alias));
        }

        await EnsureNoDuplicateDeliveriesAsync(builder, quotedTableName, providerNameColumn, deliveryIdColumn);
    }

    private static async Task EnsureNoDuplicateDeliveriesAsync(ISchemaBuilder builder,
        string quotedTableName,
        string providerNameColumn,
        string deliveryIdColumn)
    {
        // A missing provider or delivery identifier is treated as an empty value, matching how the previous
        // in-memory key was composed, so an upgrade rejects the same tenants it always did. That is stricter
        // than the unique index itself on the engines that treat nulls as distinct, which is the safe direction:
        // an upgrade refuses with repair guidance rather than creating an index that hides the ambiguity.
        var hasDuplicateDeliveries = await ContactCenterMigrationSql.ExistsAsync(
            builder,
            $"""
            SELECT 1
            FROM {quotedTableName}
            GROUP BY COALESCE({providerNameColumn}, ''), COALESCE({deliveryIdColumn}, '')
            HAVING COUNT(*) > 1
            """);

        if (hasDuplicateDeliveries)
        {
            throw new InvalidOperationException(
                "The Contact Center provider webhook inbox contains multiple messages for one provider delivery. Resolve the duplicate legacy inbox messages before enabling the provider-delivery uniqueness constraint.");
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
                    // Canonicalizes legacy provider aliases and adds the canonical provider-delivery unique constraint to
                    // existing inbox indexes.
                    var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(builder, _store, typeof(ProviderWebhookInboxMessageIndex));

                    await CanonicalizeProviderNamesAsync(builder, quotedTableName);

                    await ContactCenterMigrationSql.CreateUniqueIndexAsync(
                        builder,
                        _store,
                        typeof(ProviderWebhookInboxMessageIndex),
                        "UQ_ProviderWebhookInboxMessageIndex_Delivery",
                        "ProviderName",
                        "DeliveryId");

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
    private async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the settlement time settled deliveries are purged by. Receipt time cannot serve, because settlement
                    // lags receipt by the whole retry envelope; the retry time cannot serve either, because a settled delivery
                    // keeps whatever retry time it last held.
                    await builder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
                        .AddColumn<DateTime?>("ProcessedUtc"),
                        collection: ContactCenterStorage.CollectionName);

                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(ProviderWebhookInboxMessageIndex),
                        "ProcessedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime,
                        settledRowsFilter: $"{builder.Dialect.QuoteForColumnName("Status")} IN ({(int)ProviderWebhookInboxStatus.Completed}, {(int)ProviderWebhookInboxStatus.DeadLettered})");

                    await builder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
                        .CreateIndex(
                            "IDX_ProviderWebhookInboxMessageIndex_Retention",
                            "Status",
                            "ProcessedUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }
}
