using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Invoicing.Migrations;

/// <summary>
/// Creates the index tables of invoices and credit notes in the <c>Invoicing</c> collection.
/// </summary>
internal sealed class InvoicingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InvoiceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Number", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<int>("Status")
            .Column<int>("Settlement")
            .Column<string>("BillToKind", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("BillToId", column => column.Nullable().WithLength(InvoiceIndexProvider.PartyIdLength))
            .Column<string>("BillToName", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Reference", column => column.Nullable().WithLength(InvoiceIndexProvider.TextLength))
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<decimal>("Total")
            .Column<decimal>("AmountDue")
            .Column<DateTime>("DueDate", column => column.Nullable())
            .Column<DateTime>("IssuedUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<string>("TransactionId", column => column.Nullable().WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("SourceType", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("SourceId", column => column.Nullable().WithLength(InvoiceIndexProvider.PartyIdLength)),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_Number", "Number", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_Status", "Status", "Settlement", "DueDate", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_BillTo", "BillToKind", "BillToId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_Transaction", "TransactionId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_Source", "SourceType", "SourceId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<InvoiceIndex>(table => table
            .CreateIndex("IDX_InvoiceIndex_Created", "CreatedUtc", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<CreditNoteIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(InvoiceIndexProvider.IdLength))
            .Column<string>("Number", column => column.Nullable().WithLength(InvoiceIndexProvider.ShortLength))
            .Column<string>("InvoiceId", column => column.Nullable().WithLength(InvoiceIndexProvider.IdLength))
            .Column<int>("Status")
            .Column<string>("Currency", column => column.Nullable().WithLength(3))
            .Column<decimal>("Total")
            .Column<DateTime>("IssuedUtc", column => column.Nullable()),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CreditNoteIndex>(table => table
            .CreateIndex("IDX_CreditNoteIndex_ItemId", "ItemId", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<CreditNoteIndex>(table => table
            .CreateIndex("IDX_CreditNoteIndex_Invoice", "InvoiceId", "Status", "DocumentId"),
            collection: InvoicingConstants.CollectionName);

        return 1;
    }
}
