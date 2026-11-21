using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Ledgers;

/// <summary>
/// Creates the index table of a ledger collection.
/// </summary>
public static class QuantityLedgerIndexSchema
{
    /// <summary>
    /// Creates the index table of the ledger kept in a collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="collection">The collection of the ledger.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, string collection)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentException.ThrowIfNullOrEmpty(collection);

        await schemaBuilder.CreateMapIndexTableAsync<QuantityLedgerEntryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AccountId", column => column.Nullable().WithLength(26))
            .Column<string>("Bucket", column => column.Nullable().WithLength(64))
            .Column<string>("Kind", column => column.Nullable().WithLength(32))
            .Column<int>("EffectiveDate")
            .Column<int>("ExpiresOn", column => column.Nullable())
            .Column<string>("IdempotencyKey", column => column.Nullable().WithLength(150))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<QuantityLedgerEntryIndex>(table =>
        {
            table.CreateIndex($"IDX_{collection}_LedgerEntry_ItemId", "ItemId");
            table.CreateIndex($"IDX_{collection}_LedgerEntry_Balance", "AccountId", "Bucket", "EffectiveDate");
            table.CreateIndex($"IDX_{collection}_LedgerEntry_Key", "IdempotencyKey");
            table.CreateIndex($"IDX_{collection}_LedgerEntry_Kind", "Kind");
        }, collection: collection);
    }
}
