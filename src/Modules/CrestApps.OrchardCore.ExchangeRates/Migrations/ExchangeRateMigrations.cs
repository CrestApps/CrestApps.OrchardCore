using CrestApps.OrchardCore.ExchangeRates.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ExchangeRates.Migrations;

/// <summary>
/// Creates the exchange rate index in the <c>ExchangeRates</c> collection.
/// </summary>
public sealed class ExchangeRateMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ExchangeRateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<string>("BaseCurrency", column => column.Nullable().WithLength(3))
            .Column<DateTime>("Date")
            .Column<string>("Source", column => column.Nullable().WithLength(100)),
            collection: ExchangeRatesConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ExchangeRateIndex>(table => table
            .CreateIndex("IDX_ExchangeRateIndex_ItemId", "ItemId", "DocumentId"),
            collection: ExchangeRatesConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ExchangeRateIndex>(table => table
            .CreateIndex("IDX_ExchangeRateIndex_Pair", "Currency", "BaseCurrency", "Date", "DocumentId"),
            collection: ExchangeRatesConstants.CollectionName);

        return 1;
    }
}
