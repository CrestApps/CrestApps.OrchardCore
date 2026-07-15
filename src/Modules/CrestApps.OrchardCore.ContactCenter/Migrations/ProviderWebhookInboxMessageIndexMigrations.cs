using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the provider webhook inbox index schema and enforces canonical provider-delivery uniqueness.
/// </summary>
internal sealed class ProviderWebhookInboxMessageIndexMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxMessageIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize legacy provider aliases before duplicate preflight and unique-index creation.</param>
    public ProviderWebhookInboxMessageIndexMigrations(
        IStore store,
        IProviderIdentityResolver providerIdentityResolver)
    {
        _store = store;
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
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

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
        },
            collection: ContactCenterConstants.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ProviderWebhookInboxMessageIndex),
            "UQ_ProviderWebhookInboxMessageIndex_Delivery",
            "ProviderName",
            "DeliveryId");

        return 2;
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

    private async Task CanonicalizeProviderNamesAsync(string quotedTableName)
    {
        var documentIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("DocumentId");
        var providerNameColumn = SchemaBuilder.Dialect.QuoteForColumnName("ProviderName");
        var deliveryIdColumn = SchemaBuilder.Dialect.QuoteForColumnName("DeliveryId");

        var rows = new List<(long DocumentId, string CanonicalProviderName, string DeliveryId)>();

        await using (var selectCommand = SchemaBuilder.Connection.CreateCommand())
        {
            selectCommand.Transaction = SchemaBuilder.Transaction;
            selectCommand.CommandText =
                $"SELECT {documentIdColumn}, {providerNameColumn}, {deliveryIdColumn} FROM {quotedTableName}";

            await using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var documentId = reader.GetInt64(0);
                var providerName = reader.IsDBNull(1) ? null : reader.GetString(1);
                var deliveryId = reader.IsDBNull(2) ? null : reader.GetString(2);

                // Canonicalize the legacy provider alias before duplicate detection so alias-stored and
                // canonical deliveries for one provider collapse to a single identity that the composite
                // (ProviderName, DeliveryId) unique index can enforce.
                rows.Add((documentId, _providerIdentityResolver.Canonicalize(providerName), deliveryId));
            }
        }

        DetectDeliveryCollisions(rows);

        foreach (var row in rows)
        {
            await using var updateCommand = SchemaBuilder.Connection.CreateCommand();
            updateCommand.Transaction = SchemaBuilder.Transaction;
            updateCommand.CommandText =
                $"UPDATE {quotedTableName} SET {providerNameColumn} = @ProviderName WHERE {documentIdColumn} = @DocumentId";
            AddParameter(updateCommand, "@ProviderName", (object)row.CanonicalProviderName ?? DBNull.Value);
            AddParameter(updateCommand, "@DocumentId", row.DocumentId);
            await updateCommand.ExecuteNonQueryAsync();
        }
    }

    private static void DetectDeliveryCollisions(
        List<(long DocumentId, string CanonicalProviderName, string DeliveryId)> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = $"{row.CanonicalProviderName}\n{row.DeliveryId}";

            if (!seen.Add(key))
            {
                throw new InvalidOperationException(
                    "The Contact Center provider webhook inbox contains multiple messages for one provider delivery. Resolve the duplicate legacy inbox messages before enabling the provider-delivery uniqueness constraint.");
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
