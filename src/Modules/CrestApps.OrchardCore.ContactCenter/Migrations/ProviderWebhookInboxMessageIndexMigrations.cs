using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the provider webhook inbox index schema and enforces canonical provider-delivery uniqueness.
/// </summary>
internal sealed class ProviderWebhookInboxMessageIndexMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IClock _clock;
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public ProviderWebhookInboxMessageIndexMigrations(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver,
        IClock clock)
    {
        _store = store;
        _clock = clock;
        _providerIdentityResolver = providerIdentityResolver;
    }

    /// <summary>
    /// Creates the inbox index table and its lookup and due-message indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(ContactCenterStorage.ProviderNameLength))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<ProviderWebhookInboxStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime?>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table =>
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
            SchemaBuilder,
            _store,
            typeof(ProviderWebhookInboxMessageIndex),
            "UQ_ProviderWebhookInboxMessageIndex_Delivery",
            "ProviderName",
            "DeliveryId");

        return 3;
    }

    /// <summary>
    /// Adds the settlement time settled deliveries are purged by. Receipt time cannot serve, because settlement
    /// lags receipt by the whole retry envelope; the retry time cannot serve either, because a settled delivery
    /// keeps whatever retry time it last held.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .AddColumn<DateTime?>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(ProviderWebhookInboxMessageIndex),
            "ProcessedUtc",
            _clock.UtcNow,
            settledRowsFilter: $"{SchemaBuilder.Dialect.QuoteForColumnName("Status")} IN ({(int)ProviderWebhookInboxStatus.Completed}, {(int)ProviderWebhookInboxStatus.DeadLettered})");

        await SchemaBuilder.AlterIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .CreateIndex(
                "IDX_ProviderWebhookInboxMessageIndex_Retention",
                "Status",
                "ProcessedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Canonicalizes legacy provider aliases and adds the canonical provider-delivery unique constraint to
    /// existing inbox indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        var quotedTableName = ContactCenterMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(ProviderWebhookInboxMessageIndex));

        await CanonicalizeProviderNamesAsync(quotedTableName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ProviderWebhookInboxMessageIndex),
            "UQ_ProviderWebhookInboxMessageIndex_Delivery",
            "ProviderName",
            "DeliveryId");

        return 2;
    }

    /// <remarks>
    /// Canonicalization is a lookup over a finite alias map, so the table is asked which aliases it actually
    /// holds and each one is rewritten with a single statement. Reading every row and issuing an UPDATE per
    /// row would put a round trip per inbox message inside the transaction that gates tenant startup, which a
    /// tenant with a large delivery history never gets through.
    /// </remarks>
    private async Task CanonicalizeProviderNamesAsync(string quotedTableName)
    {
        var providerNameColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderName");
        var deliveryIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("DeliveryId");

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

            // Canonicalize the legacy provider alias before duplicate detection so alias-stored and
            // canonical deliveries for one provider collapse to a single identity that the composite
            // (ProviderName, DeliveryId) unique index can enforce.
            await ContactCenterMigrationSql.ExecuteAsync(
                SchemaBuilder,
                $"UPDATE {quotedTableName} SET {providerNameColumn} = @Canonical WHERE {providerNameColumn} = @Alias",
                ("@Canonical", canonical),
                ("@Alias", alias));
        }

        await EnsureNoDuplicateDeliveriesAsync(quotedTableName, providerNameColumn, deliveryIdColumn);
    }

    private async Task EnsureNoDuplicateDeliveriesAsync(
        string quotedTableName,
        string providerNameColumn,
        string deliveryIdColumn)
    {
        // A missing provider or delivery identifier is treated as an empty value, matching how the previous
        // in-memory key was composed, so an upgrade rejects the same tenants it always did. That is stricter
        // than the unique index itself on the engines that treat nulls as distinct, which is the safe direction:
        // an upgrade refuses with repair guidance rather than creating an index that hides the ambiguity.
        var hasDuplicateDeliveries = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
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
}
