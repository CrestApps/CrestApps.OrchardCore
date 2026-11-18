using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the late-fee policy index of the Collections feature in the <c>Invoicing</c> collection.
/// </summary>
public sealed class LateFeePolicyMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<LateFeePolicyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Name", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<bool>("Enabled"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<LateFeePolicyIndex>(table => table
            .CreateIndex("IDX_LateFeePolicyIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<LateFeePolicyIndex>(table => table
            .CreateIndex("IDX_LateFeePolicyIndex_Enabled", "Enabled", "Name", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
