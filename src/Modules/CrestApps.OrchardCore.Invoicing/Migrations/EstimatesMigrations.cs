using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the estimate index of the Estimates feature in the <c>Invoicing</c> collection.
/// </summary>
public sealed class EstimatesMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<EstimateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Number", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<int>("Status")
            .Column<string>("BillToKind", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("BillToId", column => column.Nullable().WithLength(InvoiceIndexProvider.PartyIdLength))
            .Column<string>("BillToName", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Reference", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<decimal>("Total")
            .Column<DateTime>("ExpiryDate", column => column.Nullable())
            .Column<DateTime>("SentUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<EstimateIndex>(table => table
            .CreateIndex("IDX_EstimateIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<EstimateIndex>(table => table
            .CreateIndex("IDX_EstimateIndex_Number", "Number", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<EstimateIndex>(table => table
            .CreateIndex("IDX_EstimateIndex_Status", "Status", "ExpiryDate", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<EstimateIndex>(table => table
            .CreateIndex("IDX_EstimateIndex_BillTo", "BillToKind", "BillToId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<EstimateIndex>(table => table
            .CreateIndex("IDX_EstimateIndex_Created", "CreatedUtc", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
