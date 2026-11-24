using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the billable charge index of the Billing feature in the <c>Invoicing</c> collection.
/// </summary>
public sealed class BillableItemMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<BillableItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("PartyKind", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("PartyId", column => column.Nullable().WithLength(InvoiceIndexProvider.PartyIdLength))
            .Column<string>("PartyName", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Description", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<DateTime>("Date")
            .Column<string>("InvoiceId", column => column.Nullable().WithLength(InvoiceIndexProvider.IdLength))
            .Column<bool>("IsBilled")
            .Column<DateTime>("CreatedUtc"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BillableItemIndex>(table => table
            .CreateIndex("IDX_BillableItemIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BillableItemIndex>(table => table
            .CreateIndex("IDX_BillableItemIndex_Unbilled", "PartyKind", "PartyId", "IsBilled", "Date", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
