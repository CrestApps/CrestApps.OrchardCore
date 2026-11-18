using CrestApps.OrchardCore.Transactions.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Transactions.Migrations;

/// <summary>
/// Creates the index table of the payment terms.
/// </summary>
internal sealed class PaymentTermMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PaymentTermIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(PaymentTermIndexProvider.MaxNameLength))
            .Column<bool>("Enabled"),
            collection: TransactionsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PaymentTermIndex>(table => table
            .CreateIndex("IDX_PaymentTermIndex_ItemId", "ItemId", "DocumentId"),
            collection: TransactionsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PaymentTermIndex>(table => table
            .CreateIndex("IDX_PaymentTermIndex_Enabled", "Enabled", "Name", "DocumentId"),
            collection: TransactionsConstants.CollectionName);

        return 1;
    }
}
